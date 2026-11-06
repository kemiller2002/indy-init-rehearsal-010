// Owner: persistence. PERSIST-007: a save is confirmed only by a read-back of the written bytes at its commit.
namespace Causa.Acceptance.Deterministic

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST007Tests =
    let private ops (rs: HttpRequest list) = rs |> List.choose GitHubRequests.operationOf

    [<Fact>]
    let ``PERSIST-007-O1`` () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        let head = SaveScenario.head sc
        match sc.Session.State with
        | SaveState.Saved(Confirmed c) -> Assert.Equal(head, c.CommitOid)
        | other -> failwithf "%A" other
        // The mutation returned the commit, then the files were re-read at exactly that commit.
        match ops sc.Sent with
        | [ CommitOnBranch _; ReadInvestigationAt rev ] -> Assert.Equal((let (CommitOid h) = head in h), rev)
        | other -> failwithf "%A" other
        // The bytes GitHub holds at the commit are the bytes the save wrote: the investigation reloads and verifies.
        let fs = sc.Fake.Current().Commits[(let (CommitOid h) = head in h)].Tree
        let d = SaveKit.dir SaveKit.investigationId
        let verified =
            StorageReader.verify Kernel.domain (Some SaveKit.investigationId) SaveKit.address head
                { Manifest = fs[d + "/manifest.json"]; Events = fs[d + "/events.ndjson"]; Snapshot = fs[d + "/snapshot.json"] }
        Assert.True(Result.isOk verified)
        // A re-read whose bytes differ from the written bytes is never a confirmation.
        let sc2 = SaveScenario.create ()
        SaveScenario.edit sc2 1
        sc2.Before <-
            fun r ->
                match GitHubRequests.operationOf r with
                | Some(ReadInvestigationAt rev) when not (rev.StartsWith "refs/") ->
                    sc2.Fake.Update(fun s ->
                        let c = s.Commits[rev]
                        let path = d + "/events.ndjson"
                        { s with Commits = s.Commits.Add(rev, { c with Tree = c.Tree.Add(path, Array.append c.Tree[path] "\n"B) }) })
                | _ -> ()
        SaveScenario.input sc2 SaveInput.SaveRequested |> ignore
        match sc2.Session.State with
        | SaveState.SaveFailed(BytesDiffer c) -> Assert.Equal(SaveScenario.head sc2, c)
        | other -> failwithf "%A" other
        // Files too large for one GraphQL answer are re-read through the Git blobs API, and still confirm.
        let sc3 = SaveScenario.create ()
        sc3.Fake.Update(fun s -> { s with TruncateTextAbove = 64 })
        SaveScenario.edit sc3 1
        SaveScenario.input sc3 SaveInput.SaveRequested |> ignore
        Assert.Equal("Saved", SaveScenario.stateName sc3)
        Assert.Equal(3, sc3.Sent |> List.filter (fun r -> r.Url.Contains "/git/blobs/") |> List.length)

    [<Fact>]
    let ``PERSIST-007-O2`` () =
        let sc = SaveScenario.create ()
        let baseHead = sc.Session.BaseHead
        SaveScenario.edit sc 1
        // The mutation is applied but its read-back cannot be read: not Saved, base head not advanced.
        sc.Before <-
            fun r ->
                match GitHubRequests.operationOf r with
                | Some(ReadInvestigationAt _) -> sc.Fake.Update(StatefulFake.injectFault NextRequest TimeOutBeforeApplying)
                | _ -> ()
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        Assert.Equal("UnknownOutcome", SaveScenario.stateName sc)
        Assert.Equal(baseHead, sc.Session.BaseHead)
        // Reconciliation finds this save's marker on the written bytes: only then Saved, and the base head advances.
        sc.Before <- ignore
        SaveScenario.reconcileDue sc |> ignore
        match sc.Session.State with
        | SaveState.Saved(Confirmed c) ->
            Assert.Equal(SaveScenario.head sc, c.CommitOid)
            Assert.Equal(c.CommitOid, sc.Session.BaseHead)
        | other -> failwithf "%A" other

    [<Fact>]
    let ``PERSIST-007-O3`` () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        let next, effects = SaveKit.stepOk sc.Machine sc.Clock sc.Session SaveInput.SaveRequested
        // An edit while the save is in flight.
        let during, _ = SaveKit.stepOk sc.Machine sc.Clock next (SaveInput.Edit(SaveKit.edit 2))
        let final, _, _ = SaveKit.drive sc.Machine sc.Clock (SaveScenario.execute sc) during (SaveKit.requests effects)
        Assert.Equal("Unsaved", MachineKit.stateName final.State)
        Assert.Equal<CommandEnvelope list>([ SaveKit.edit 2 ], final.Unsaved)
        Assert.Equal(SaveScenario.head sc, final.BaseHead)

    [<Fact>]
    let ``PERSIST-007-O4`` () =
        // PAT-12: over every state and input, Saved is entered only from a read-back confirmation (or the approved
        // verified discard), and that Saved carries the very confirmation it was given.
        let m = MachineKit.m ()
        let sessions =
            [ MachineKit.saved (); MachineKit.unsaved (); MachineKit.saving (ReadBackAt(CommitOid "c1")); MachineKit.offline ()
              MachineKit.conflict (Discarded MachineKit.remoteVerified); MachineKit.unknown 0 3 (Some(ReconcileFilesAt(CommitOid "c1")))
              MachineKit.failed () ]
        let c1 = MachineKit.confirmation "c1"
        let inputs =
            [ SaveInput.Edit(SaveKit.edit 9); SaveInput.SaveRequested; SaveInput.ReadBackMatched c1; SaveInput.ReadBackMismatched(CommitOid "c1")
              SaveInput.ReconcileMarkerBytesMatch c1; SaveInput.ConflictResolvedDiscarded MachineKit.remoteVerified; SaveInput.WentOnline
              SaveInput.CheckAgain; SaveInput.ReconcileNothingWrittenHeadUnchanged; SaveInput.QueuedSaveStarted ]
        for s in sessions do
            for i in inputs do
                match m.Step MachineKit.ctx s i, s.State with
                | Ok(next, _), prior when MachineKit.stateName prior <> "Saved" ->
                    match next.State, i with
                    | SaveState.Saved(Confirmed c), SaveInput.ReadBackMatched x
                    | SaveState.Saved(Confirmed c), SaveInput.ReconcileMarkerBytesMatch x -> Assert.Equal(x, c)
                    | SaveState.Saved(VerifiedAt v), SaveInput.ConflictResolvedDiscarded x -> Assert.Equal(x, v)
                    | SaveState.Saved _, _ -> failwithf "%A reached Saved from %A" i prior
                    | _ -> ()
                | _ -> ()
        // PAT-10: after a save, a fresh load shows only what GitHub holds (the confirmed commit).
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        let reload = SaveScenario.create ()
        let r: InvestigationRef = { Address = SaveKit.address; Id = SaveKit.investigationId }
        match reload.Machine.StepLoad (SaveKit.context reload.Clock) NotLoaded (LoadInput.OpenRequested r) with
        | Ok(state, effects) ->
            match reload.Machine.InterpretLoad (SaveKit.context reload.Clock) state (sc.Fake.Execute (SaveKit.requests effects).Head) with
            | LoadEvent(LoadInput.LoadVerified v) ->
                Assert.Equal(SaveScenario.head sc, v.ReadAt)
                Assert.Equal(AggregateVersion 2L, v.Investigation.Version)
            | other -> failwithf "%A" other
        | Error f -> failwithf "%A" f
