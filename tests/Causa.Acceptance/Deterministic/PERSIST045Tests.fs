// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// PERSIST-045: a lost mutation answer, after and before GitHub applied it, reconciled before anything is sent again.
namespace Causa.Acceptance.Deterministic

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST045Tests =
    /// One save whose mutation answer is lost in the given way.
    let private lost (fault: FakeFault) =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        sc.Fake.Update(StatefulFake.injectFault NextMutation fault)
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        sc

    let private headAt (sc: SaveScenario.T) = SaveScenario.head sc

    [<Fact>]
    let ``PERSIST-045-O1`` () =
        // After applying: the commit is on the branch although no answer came back.
        let after = lost LoseAnswerAfterApplying
        Assert.NotEqual(after.Session.BaseHead, headAt after)
        // Before applying: nothing was written.
        let before = lost LoseAnswerBeforeApplying
        Assert.Equal(before.Session.BaseHead, headAt before)
        for sc in [ after; before ] do
            Assert.Contains(SaveInput.AnswerLost, sc.Events)
            Assert.Equal(1, (SaveScenario.mutations sc).Length)

    [<Fact>]
    let ``PERSIST-045-O2`` () =
        for fault in [ LoseAnswerAfterApplying; LoseAnswerBeforeApplying ] do
            let sc = lost fault
            Assert.Equal("UnknownOutcome", SaveScenario.stateName sc)
            // No second mutation before reconciliation: a save request is refused, edits send nothing.
            Assert.True(Result.isError (sc.Machine.Step (SaveKit.context sc.Clock) sc.Session SaveInput.SaveRequested))
            SaveScenario.edit sc 2
            Assert.Equal(1, (SaveScenario.mutations sc).Length)
            // The first request after the loss is the reconciliation read of the commits after the expected head.
            let expected = sc.Session.BaseHead
            SaveScenario.reconcileDue sc |> ignore
            let afterLoss = sc.Sent |> List.skipWhile (SaveKit.isMutation >> not) |> List.tail
            Assert.Equal(Some(CompareFrom expected), afterLoss |> List.tryHead |> Option.bind GitHubRequests.operationOf)

    [<Fact>]
    let ``PERSIST-045-O3`` () =
        let sc = lost LoseAnswerAfterApplying
        SaveScenario.reconcileDue sc |> ignore
        match sc.Session.State with
        | SaveState.Saved(Confirmed c) -> Assert.Equal(headAt sc, c.CommitOid)
        | other -> failwithf "%A" other
        Assert.Contains(sc.Events, fun e -> match e with SaveInput.ReconcileMarkerBytesMatch _ -> true | _ -> false)
        Assert.Equal(1, (SaveScenario.mutations sc).Length)

    [<Fact>]
    let ``PERSIST-045-O4`` () =
        let sc = lost LoseAnswerBeforeApplying
        let expected = sc.Session.BaseHead
        SaveScenario.reconcileDue sc |> ignore
        Assert.Contains(SaveInput.ReconcileNothingWrittenHeadUnchanged, sc.Events)
        Assert.Equal("Saved", SaveScenario.stateName sc)
        let mutations = SaveScenario.mutations sc
        Assert.True(mutations.Length = 2 && mutations.Length <= 4)
        Assert.All(mutations, fun r -> Assert.Equal(Some(CommitOnBranch expected), GitHubRequests.operationOf r))

    [<Fact>]
    let ``PERSIST-045-O5`` () =
        // Moved by an unrelated commit: rebased and saved (stale-head rules, PERSIST-009).
        let sc = lost LoseAnswerBeforeApplying
        SaveScenario.otherWriter sc [ "unrelated/a.json", "{}"B ]
        SaveScenario.reconcileDue sc |> ignore
        Assert.Contains(sc.Events, fun e -> match e with SaveInput.ReconcileNothingWrittenHeadMoved _ -> true | _ -> false)
        Assert.Equal("Saved", SaveScenario.stateName sc)
        // Moved by a change to the same investigation: a conflict (PERSIST-010).
        let sc2 = lost LoseAnswerBeforeApplying
        SaveScenario.otherWriter sc2 (SaveKit.repoFiles SaveKit.investigationId (SaveKit.filesOf MachineKit.remoteHistory))
        SaveScenario.reconcileDue sc2 |> ignore
        Assert.Equal("Conflict", SaveScenario.stateName sc2)

    [<Fact>]
    let ``PERSIST-045-O6`` () =
        // A marker with different bytes.
        let differ = lost LoseAnswerAfterApplying
        let head = (let (CommitOid h) = headAt differ in h)
        let path = SaveKit.dir SaveKit.investigationId + "/snapshot.json"
        differ.Fake.Update(fun s -> let c = s.Commits[head] in { s with Commits = s.Commits.Add(head, { c with Tree = c.Tree.Add(path, "{}\n"B) }) })
        SaveScenario.reconcileDue differ |> ignore
        Assert.Equal(SaveState.SaveFailed(BytesDiffer(CommitOid head)), differ.Session.State)
        // A rewritten history.
        let rewritten = lost LoseAnswerBeforeApplying
        rewritten.Fake.Update(fun s -> StatefulFake.rewriteHistory "main" s |> fst)
        SaveScenario.reconcileDue rewritten |> ignore
        Assert.Equal(SaveState.SaveFailed HistoryWasRewritten, rewritten.Session.State)
        // A failing reconciliation read.
        let failing = lost LoseAnswerAfterApplying
        failing.Fake.Update(StatefulFake.injectFault NextRequest TimeOutBeforeApplying)
        SaveScenario.reconcileDue failing |> ignore
        Assert.Contains(SaveInput.ReconcileReadFailed, failing.Events)
        Assert.Equal("UnknownOutcome", SaveScenario.stateName failing)
        // Every case keeps the unsaved change.
        for sc in [ differ; rewritten; failing ] do
            Assert.Contains(SaveKit.edit 1, sc.Session.Unsaved @ sc.Session.AfterInFlight)

    [<Fact>]
    let ``PERSIST-045-O7`` () =
        // The whole rule, end to end on the candidate build: every branch reaches its expected state.
        let outcome (setup: SaveScenario.T -> unit) (fault: FakeFault) =
            let sc = lost fault
            setup sc
            SaveScenario.reconcileDue sc |> ignore
            SaveScenario.stateName sc
        Assert.Equal("Saved", outcome ignore LoseAnswerAfterApplying)
        Assert.Equal("Saved", outcome ignore LoseAnswerBeforeApplying)
        Assert.Equal("Saved", outcome (fun sc -> SaveScenario.otherWriter sc [ "u.json", "{}"B ]) LoseAnswerBeforeApplying)
        Assert.Equal("SaveFailed", outcome (fun sc -> sc.Fake.Update(fun s -> StatefulFake.rewriteHistory "main" s |> fst)) LoseAnswerBeforeApplying)
        Assert.Equal("UnknownOutcome", outcome (fun sc -> sc.Fake.Update(StatefulFake.injectFault NextRequest LoseAnswerBeforeApplying)) LoseAnswerAfterApplying)

    [<Fact>]
    let ``PERSIST-045-O8`` () =
        // PERSIST-015: reconciliation reads the commits after the expected head and finds this save's marker there.
        let sc = lost LoseAnswerAfterApplying
        let mutation = (SaveScenario.mutations sc).Head
        let (CommitOid expected) =
            match GitHubRequests.operationOf mutation with
            | Some(CommitOnBranch e) -> e
            | other -> failwithf "%A" other
        let marker =
            use doc = System.Text.Json.JsonDocument.Parse(mutation.Body.Value)
            GitHubRequests.markerOf (string (doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("message").GetProperty("body").GetString()))
        Assert.True(marker.IsSome)
        SaveScenario.reconcileDue sc |> ignore
        let compares = sc.Sent |> List.filter (fun r -> r.Url.Contains "/compare/")
        Assert.NotEmpty(compares)
        Assert.All(compares, fun r -> Assert.Contains("/compare/" + expected + "...", r.Url))
        let head = SaveScenario.head sc
        let message = (sc.Fake.Current()).Commits[(let (CommitOid h) = head in h)].Message
        Assert.Equal(marker, GitHubRequests.markerOf message)
        Assert.Equal("Saved", SaveScenario.stateName sc)
