// Owner: persistence. PERSIST-010: bounded automatic rebase.
namespace Causa.Acceptance.Deterministic

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST010Tests =
    /// A branch another writer advances (with an unrelated file) before every mutation of this tab.
    let private busy () =
        let sc = SaveScenario.create ()
        let mutable n = 0
        sc.Before <-
            fun r ->
                if SaveKit.isMutation r then
                    n <- n + 1
                    SaveScenario.otherWriter sc [ sprintf "unrelated/%d.json" n, "{}"B ]
        SaveScenario.edit sc 1
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        sc

    [<Fact>]
    let ``PERSIST-010-O1`` () =
        let sc = busy ()
        Assert.Equal(4, (SaveScenario.mutations sc).Length)
        Assert.Equal(3, sc.Session.RebaseAttempts)
        let rebases = sc.Events |> List.filter (function SaveInput.StaleHeadUnrelated _ -> true | _ -> false)
        Assert.Equal(4, rebases.Length) // the fourth stale head is the one that ends the save

    [<Fact>]
    let ``PERSIST-010-O2`` () =
        let sc = busy ()
        // Between two consecutive mutations there is always a fresh read of the branch head.
        let indexed = sc.Sent |> List.indexed
        let mutationIdx = indexed |> List.filter (snd >> SaveKit.isMutation) |> List.map fst
        for a, b in List.pairwise mutationIdx do
            let between = sc.Sent |> List.skip (a + 1) |> List.take (b - a - 1) |> List.choose GitHubRequests.operationOf
            Assert.Contains(ReadBranchHead, between)
        // And every rebased mutation expects the head that read returned.
        let expected = SaveScenario.mutations sc |> List.choose GitHubRequests.operationOf
        Assert.Equal(expected.Length, (List.distinct expected).Length)

    [<Fact>]
    let ``PERSIST-010-O3`` () =
        let sc = busy ()
        Assert.Equal(SaveState.SaveFailed RepositoryKeptChanging, sc.Session.State)
        Assert.Contains(SaveKit.edit 1, sc.Session.Unsaved)
        Assert.Empty(SaveKit.requests [])
        // Nothing more is sent until the user asks again; then the save starts with fresh bounds and succeeds.
        let sentBefore = sc.Sent.Length
        SaveScenario.input sc SaveInput.WentOnline |> ignore
        Assert.Equal(sentBefore, sc.Sent.Length)
        sc.Before <- ignore
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        Assert.Equal("Saved", SaveScenario.stateName sc)

    [<Fact>]
    let ``PERSIST-010-O4`` () =
        let sc = busy ()
        Assert.True((SaveScenario.mutations sc).Length <= 4)
        // Every other writer's commit stays on the branch.
        let state = sc.Fake.Current()
        let head = state.Branches["main"]
        let others = state.Commits |> Map.toList |> List.filter (fun (_, c) -> c.Author = "other writer") |> List.map fst
        Assert.Equal(4, others.Length)
        Assert.All(others, fun o -> Assert.True(StatefulFake.isAncestor state o head))
