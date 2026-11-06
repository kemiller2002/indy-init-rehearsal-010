// Owner: persistence. SM-CONFLICT transition tests (state-machine-contracts.json).
namespace Causa.Persistence.Tests.StateMachines

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport
open Causa.TestSupport.MachineKit

module SMCONFLICT =
    let private conflictStep (view: ConflictView) (input: ConflictInput) =
        match (m ()).StepConflict ctx view input with
        | Ok x -> x
        | Error f -> failwithf "%s/%s refused: %s" f.State f.Input f.Reason

    let private viewName (v: ConflictView) =
        match v with
        | ReadingRemote _ -> "ReadingRemote"
        | Reviewable _ -> "Reviewable"
        | Reapplying _ -> "Reapplying"
        | Reapplied _ -> "Reapplied"
        | ConfirmingDiscard _ -> "ConfirmingDiscard"
        | Discarded _ -> "Discarded"
        | RemoteUnavailable -> "RemoteUnavailable"
        | Unresolvable _ -> "Unresolvable"

    let private noWrites (effects: SaveEffect list) = Assert.Empty(mutations effects)

    /// A conflict started from a save (T-S16), so the machine knows which investigation is under review.
    let private started () =
        let next, effects = step ctx (saving (FileComparisonAt(CommitOid "c2"))) (SaveInput.StaleHeadSameInvestigation(CommitOid "c2"))
        next, effects

    [<Fact>]
    let ``T-C01`` () =
        // The conflict read against the fake: another writer changed the investigation; GitHub's version verifies.
        let running, baseV = SaveKit.repository ()
        let c = SaveKit.clock ()
        let session = (m ()).Initial SaveKit.writable baseV
        running.Update(fun s ->
            let files = SaveKit.repoFiles SaveKit.investigationId (SaveKit.filesOf remoteHistory)
            StatefulFake.otherWriterCommits "main" "other tab" files [] s |> fst)
        let edited, _ = SaveKit.stepOk (m ()) c session (SaveInput.Edit(SaveKit.edit 1))
        let saving, effects = SaveKit.stepOk (m ()) c edited SaveInput.SaveRequested
        let final, events, _ = SaveKit.drive (m ()) c running.Execute saving (SaveKit.requests effects)
        Assert.Contains(events, fun e -> match e with SaveInput.StaleHeadSameInvestigation _ -> true | _ -> false)
        match final.State with
        | SaveState.Conflict(ReadingRemote id) ->
            let read = c.Next - 1L
            Assert.Equal(RequestId read, id)
        | other -> failwithf "%A" other
        // The re-read request was the last one sent; its answer verifies into Reviewable.
        let view = match final.State with SaveState.Conflict v -> v | _ -> failwith "conflict"
        let reread = GitHubRequests.investigationFiles (match view with ReadingRemote id -> id | _ -> RequestId 0L) SaveKit.connection GitHubRequests.BranchHead (RepoPath(SaveKit.dir SaveKit.investigationId))
        match (m ()).InterpretConflict ctx view (running.Execute reread) with
        | ConflictEvent(ConflictInput.RemoteVerified v) ->
            let next, effects = conflictStep view (ConflictInput.RemoteVerified v)
            Assert.Equal(Reviewable v, next)
            Assert.Equal(AggregateVersion 2L, v.Investigation.Version)
            noWrites effects
        | other -> failwithf "%A" other
        Assert.Contains(SaveKit.edit 1, work final)

    [<Fact>]
    let ``T-C02`` () =
        let next, effects = conflictStep (ReadingRemote(RequestId 7L)) (ConflictInput.RemoteReadFailed LostAfterSending)
        Assert.Equal(RemoteUnavailable, next)
        noWrites effects

    [<Fact>]
    let ``T-C03`` () =
        let next, effects = conflictStep (ReadingRemote(RequestId 7L)) (ConflictInput.RemoteMissingOrInvalid None)
        Assert.Equal("Unresolvable", viewName next)
        noWrites effects

    [<Fact>]
    let ``T-C14`` () =
        let view = ReadingRemote(RequestId 7L)
        Assert.Equal(view, fst (conflictStep view (ConflictInput.SupersededReply(RequestId 3L))))
        match (m ()).InterpretConflict ctx view (NotAnswered(RequestId 3L, LostAfterSending)) with
        | ConflictEvent(ConflictInput.SupersededReply id) -> Assert.Equal(RequestId 3L, id)
        | other -> failwithf "%A" other

    [<Fact>]
    let ``T-C11`` () =
        let next, effects = conflictStep (Reviewable remoteVerified) ConflictInput.ReadAgain
        Assert.Equal(ReadingRemote(RequestId 40L), next)
        Assert.Equal(Some(ReadInvestigationAt "refs/heads/main"), GitHubRequests.operationOf (sends effects).Head)
        noWrites effects

    [<Fact>]
    let ``T-C05`` () =
        let view = Reviewable remoteVerified
        let next, effects = conflictStep view ConflictInput.ReviewChosen
        Assert.Equal(view, next)
        noWrites effects

    [<Fact>]
    let ``T-C06`` () =
        let next, effects = conflictStep (Reviewable remoteVerified) ConflictInput.ReapplyChosen
        Assert.Equal(Reapplying remoteVerified, next)
        noWrites effects

    [<Fact>]
    let ``T-C08`` () =
        let next, effects = conflictStep (Reviewable remoteVerified) ConflictInput.DiscardChosen
        Assert.Equal(ConfirmingDiscard remoteVerified, next)
        Assert.Contains(effects, fun e -> match e with Announce _ -> true | _ -> false)
        noWrites effects

    [<Fact>]
    let ``T-C15`` () =
        let view = Reviewable remoteVerified
        Assert.Equal(view, fst (conflictStep view (ConflictInput.SupersededReply(RequestId 3L))))

    [<Fact>]
    let ``T-C07`` () =
        let refused = [ { Command = SaveKit.edit 2; Reason = "the engine refused it" } ]
        let next, effects = conflictStep (Reapplying remoteVerified) (ConflictInput.ReapplyDone([ SaveKit.edit 1 ], refused))
        Assert.Equal(Reapplied(remoteVerified, refused), next)
        noWrites effects
        // The accepted commands become unsaved changes on the GitHub version's commit; nothing is saved.
        let session, saveEffects = step ctx (conflict next) (SaveInput.ConflictResolvedReapplied [ SaveKit.edit 1 ])
        Assert.Equal(remoteVerified.ReadAt, session.BaseHead)
        Assert.Equal("Unsaved", stateName session.State)
        Assert.Empty(sends saveEffects)

    [<Fact>]
    let ``T-C09`` () =
        let next, effects = conflictStep (ConfirmingDiscard remoteVerified) ConflictInput.DiscardConfirmed
        Assert.Equal(Discarded remoteVerified, next)
        noWrites effects

    [<Fact>]
    let ``T-C10`` () =
        Assert.Equal(Reviewable remoteVerified, fst (conflictStep (ConfirmingDiscard remoteVerified) ConflictInput.DiscardCancelled))

    [<Fact>]
    let ``T-C04`` () =
        started () |> ignore
        let next, effects = conflictStep RemoteUnavailable ConflictInput.ReadAgain
        Assert.Equal(ReadingRemote(RequestId 40L), next)
        Assert.Equal(1, (sends effects).Length)
        noWrites effects

    [<Fact>]
    let ``T-C13`` () =
        started () |> ignore
        let next, effects = conflictStep (Unresolvable "deleted") ConflictInput.ReadAgain
        Assert.Equal(ReadingRemote(RequestId 40L), next)
        noWrites effects

    [<Fact>]
    let ``T-C12`` () =
        let view = Unresolvable "deleted"
        let next, effects = conflictStep view ConflictInput.ExportChosen
        Assert.Equal(view, next)
        Assert.Contains(effects, fun e -> match e with ExportCommands _ -> true | _ -> false)
        Assert.Empty(sends effects)

    [<Fact>]
    let EveryIllegalPairFailsClosed () =
        let views =
            [ ReadingRemote(RequestId 7L); Reviewable remoteVerified; Reapplying remoteVerified; Reapplied(remoteVerified, [])
              ConfirmingDiscard remoteVerified; Discarded remoteVerified; RemoteUnavailable; Unresolvable "x" ]
        let inputs =
            [ "RemoteVerified", ConflictInput.RemoteVerified remoteVerified
              "RemoteReadFailed", ConflictInput.RemoteReadFailed LostAfterSending
              "RemoteMissingOrInvalid", ConflictInput.RemoteMissingOrInvalid None
              "ReadAgain", ConflictInput.ReadAgain
              "ReviewChosen", ConflictInput.ReviewChosen
              "ReapplyChosen", ConflictInput.ReapplyChosen
              "ReapplyDone", ConflictInput.ReapplyDone([], [])
              "DiscardChosen", ConflictInput.DiscardChosen
              "DiscardConfirmed", ConflictInput.DiscardConfirmed
              "DiscardCancelled", ConflictInput.DiscardCancelled
              "ExportChosen", ConflictInput.ExportChosen
              "SupersededReply", ConflictInput.SupersededReply(RequestId 3L) ]
        let legal =
            set
                [ "ReadingRemote", "RemoteVerified"; "ReadingRemote", "RemoteReadFailed"; "ReadingRemote", "RemoteMissingOrInvalid"
                  "ReadingRemote", "SupersededReply"; "Reviewable", "ReadAgain"; "Reviewable", "ReviewChosen"; "Reviewable", "ReapplyChosen"
                  "Reviewable", "DiscardChosen"; "Reviewable", "SupersededReply"; "Reapplying", "ReapplyDone"
                  "ConfirmingDiscard", "DiscardConfirmed"; "ConfirmingDiscard", "DiscardCancelled"; "RemoteUnavailable", "ReadAgain"
                  "Unresolvable", "ReadAgain"; "Unresolvable", "ExportChosen" ]
        let illegal = [ for v in views do for i, input in inputs do if not (legal.Contains((viewName v, i))) then yield v, i, input ]
        Assert.Equal(81, illegal.Length)
        for v, i, input in illegal do
            match (m ()).StepConflict ctx v input with
            | Ok(next, _) -> failwithf "%s + %s accepted, reached %s" (viewName v) i (viewName next)
            | Error f ->
                Assert.Equal("SM-CONFLICT", f.Machine)
                Assert.Equal(viewName v, f.State)
                Assert.Equal(i, f.Input)
