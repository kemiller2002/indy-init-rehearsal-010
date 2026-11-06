// Owner: persistence. PERSIST-015: an unknown commit outcome is reconciled by marker and head before any resend.
namespace Causa.Acceptance.Deterministic

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST015Tests =
    /// A save whose mutation meets `fault`.
    let private lost (fault: FakeFault) =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        sc.Fake.Update(StatefulFake.injectFault NextMutation fault)
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        sc

    [<Fact>]
    let ``PERSIST-015-O1`` () =
        let sc = lost LoseAnswerAfterApplying
        Assert.Equal("UnknownOutcome", SaveScenario.stateName sc)
        Assert.Contains(SaveInput.AnswerLost, sc.Events)

    [<Fact>]
    let ``PERSIST-015-O2`` () =
        for fault in
            [ TimeOutBeforeApplying
              LoseAnswerAfterApplying
              LoseAnswerBeforeApplying
              AnswerWith(502, [], "<html>Bad Gateway</html>")
              AnswerWith(200, Recordings.standardHeaders 4000, """{"data":{"createCommitOnBranch":null},"errors":[{"type":"SOMETHING_NEW","message":"?"}]}""")
              AnswerWith(200, Recordings.standardHeaders 4000, "not json") ] do
            let sc = lost fault
            Assert.True((SaveScenario.stateName sc = "UnknownOutcome"), sprintf "%A reached %s" fault (SaveScenario.stateName sc))

    [<Fact>]
    let ``PERSIST-015-O3`` () =
        let sc = lost LoseAnswerAfterApplying
        let mutations = (SaveScenario.mutations sc).Length
        // No further mutation until reconciliation decides: a save request is refused, edits send nothing.
        Assert.True(Result.isError (sc.Machine.Step (SaveKit.context sc.Clock) sc.Session SaveInput.SaveRequested))
        SaveScenario.edit sc 2
        Assert.Equal(mutations, (SaveScenario.mutations sc).Length)
        Assert.Equal("UnknownOutcome", SaveScenario.stateName sc)

    [<Fact>]
    let ``PERSIST-015-O4`` () =
        let sc = lost LoseAnswerBeforeApplying
        let expected = sc.Session.BaseHead
        let effects = SaveScenario.reconcileDue sc
        let first = (SaveKit.requests effects).Head
        // The reconciliation read is the commits after the expected head, up to the branch head.
        Assert.Equal(Some(CompareFrom expected), GitHubRequests.operationOf first)
        Assert.Contains("/compare/", first.Url)
        Assert.EndsWith("...main?per_page=100&page=1", first.Url)

    [<Fact>]
    let ``PERSIST-015-O5`` () =
        let sc = lost LoseAnswerAfterApplying
        SaveScenario.reconcileDue sc |> ignore
        match sc.Session.State with
        | SaveState.Saved(Confirmed c) -> Assert.Equal(SaveScenario.head sc, c.CommitOid)
        | other -> failwithf "%A" other
        Assert.Equal(1, (SaveScenario.mutations sc).Length)

    [<Fact>]
    let ``PERSIST-015-O6`` () =
        let sc = lost LoseAnswerAfterApplying
        // GitHub's commit carries this save's marker but holds other bytes.
        let head = (let (CommitOid h) = SaveScenario.head sc in h)
        let path = SaveKit.dir SaveKit.investigationId + "/snapshot.json"
        sc.Fake.Update(fun s ->
            let c = s.Commits[head]
            { s with Commits = s.Commits.Add(head, { c with Tree = c.Tree.Add(path, "{}\n"B) }) })
        SaveScenario.reconcileDue sc |> ignore
        Assert.Equal(SaveState.SaveFailed(BytesDiffer(CommitOid head)), sc.Session.State)
        Assert.Contains(SaveKit.edit 1, sc.Session.Unsaved)

    [<Fact>]
    let ``PERSIST-015-O7`` () =
        let sc = lost LoseAnswerBeforeApplying
        let expected = sc.Session.BaseHead
        Assert.Equal(expected, SaveScenario.head sc)
        SaveScenario.reconcileDue sc |> ignore
        Assert.Contains(SaveInput.ReconcileNothingWrittenHeadUnchanged, sc.Events)
        Assert.Equal("Saved", SaveScenario.stateName sc)
        let mutations = SaveScenario.mutations sc
        Assert.Equal(2, mutations.Length)
        Assert.All(mutations, fun r -> Assert.Equal(Some(CommitOnBranch expected), GitHubRequests.operationOf r))

    [<Fact>]
    let ``PERSIST-015-O8`` () =
        // Unrelated commits moved the head: the stale-head rules rebase.
        let sc = lost LoseAnswerBeforeApplying
        SaveScenario.otherWriter sc [ "unrelated/a.json", "{}"B ]
        SaveScenario.reconcileDue sc |> ignore
        Assert.Contains(sc.Events, fun e -> match e with SaveInput.ReconcileNothingWrittenHeadMoved _ -> true | _ -> false)
        Assert.Equal("Saved", SaveScenario.stateName sc)
        // The same investigation changed: the stale-head rules make it a conflict.
        let sc2 = lost LoseAnswerBeforeApplying
        SaveScenario.otherWriter sc2 (SaveKit.repoFiles SaveKit.investigationId (SaveKit.filesOf MachineKit.remoteHistory))
        SaveScenario.reconcileDue sc2 |> ignore
        Assert.Equal("Conflict", SaveScenario.stateName sc2)

    [<Fact>]
    let ``PERSIST-015-O9`` () =
        let sc = lost LoseAnswerBeforeApplying
        sc.Fake.Update(fun s -> StatefulFake.rewriteHistory "main" s |> fst)
        SaveScenario.reconcileDue sc |> ignore
        Assert.Equal(SaveState.SaveFailed HistoryWasRewritten, sc.Session.State)
        Assert.Contains(SaveKit.edit 1, sc.Session.Unsaved)

    [<Fact>]
    let ``PERSIST-015-O10`` () =
        let sc = lost LoseAnswerAfterApplying
        sc.Fake.Update(StatefulFake.injectFault NextRequest TimeOutBeforeApplying)
        SaveScenario.reconcileDue sc |> ignore
        Assert.Contains(SaveInput.ReconcileReadFailed, sc.Events)
        Assert.Equal("UnknownOutcome", SaveScenario.stateName sc)

    [<Fact>]
    let ``PERSIST-015-O11`` () =
        // PAT-06: each branch of the rule with an injected lost answer.
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
    let ReconcileBeyond250Commits () =
        // The answer is lost before applying; then 300 other commits land. Reconciliation pages through every commit
        // after the expected head (GitHub lists at most 250 at once) before it rules the marker out, in one read.
        let sc = lost LoseAnswerBeforeApplying
        for i in 1..300 do
            SaveScenario.otherWriter sc [ sprintf "busy/%d.json" i, "{}"B ]
        SaveScenario.reconcileDue sc |> ignore
        let pages = sc.Sent |> List.filter (fun r -> r.Url.Contains "/compare/") |> List.map (fun r -> r.Url.Substring(r.Url.LastIndexOf "page=" + 5))
        Assert.Equal<string list>([ "1"; "2"; "3" ], pages)
        Assert.Equal(1, sc.Session.ReconcileReadsSentAt.Length)
        Assert.Equal("Saved", SaveScenario.stateName sc)
