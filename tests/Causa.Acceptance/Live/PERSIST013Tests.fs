// Owner: persistence. PERSIST-013 (live): no write removes, replaces or hides another writer's commit.
namespace Causa.Acceptance.Live

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST013Tests =
    /// A sequence of saves, an unrelated rebase and a same-investigation conflict with discard, with other writers.
    let private sequence (create: string -> LiveRun.T) (label: string) =
        let run = create label
        let others = ResizeArray<CommitOid>()
        LiveRun.edit run "first" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        LiveRun.edit run "second" |> ignore
        others.Add(Probes.otherWriter run.Provider (sprintf "unrelated/%s.json" label) "{}")
        LiveRun.input run SaveInput.SaveRequested |> ignore
        // Another tab saves the same investigation; this tab's next save is a conflict, then discards.
        let tabB = { run with Machine = SaveMachines.create SaveMachines.support; Session = run.Session; Sent = []; Events = [] }
        match LiveRun.openInNewTab run with
        | Ok v ->
            tabB.Session <- (tabB.Machine.Initial run.Writable v)
            LiveRun.edit tabB "tab B" |> ignore
            LiveRun.input tabB SaveInput.SaveRequested |> ignore
            others.Add tabB.Session.BaseHead
        | Error f -> failwithf "%A" f
        LiveRun.edit run "third" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        run, List.ofSeq others

    let o1 (create: string -> LiveRun.T) =
        let run, others = sequence create "persist013-o1"
        Assert.Equal("Conflict", MachineKit.stateName run.Session.State)
        for o in others do
            Assert.Equal(Ok true, Scenarios.reachable run.Provider o)

    let o2 (create: string -> LiveRun.T) =
        let run, _ = sequence create "persist013-o2"
        // Only createCommitOnBranch writes, each against a head this tab read from GitHub in this session: the head
        // read when the investigation was created (the first mutation's), a head re-read after a stale answer, a
        // commit whose files it read, or a commit it confirmed by read-back.
        let mutations = LiveRun.mutations run |> List.choose GitHubRequests.operationOf
        let expectedOf = function CommitOnBranch(CommitOid e) -> e | other -> failwithf "%A" other
        let readRevs = run.Sent |> List.choose GitHubRequests.operationOf |> List.choose (function ReadInvestigationAt rev -> Some rev | _ -> None)
        let confirmed = run.Events |> List.choose (function SaveInput.ReadBackMatched c -> Some(let (CommitOid o) = c.CommitOid in o) | _ -> None)
        let fresh = run.Events |> List.choose (function SaveInput.StaleHeadUnrelated(CommitOid o) -> Some o | _ -> None)
        let known = set ((expectedOf mutations.Head) :: readRevs @ confirmed @ fresh)
        for m in mutations do
            Assert.True(known.Contains(expectedOf m), sprintf "a mutation expected a head this tab never read: %s" (expectedOf m))
        Assert.DoesNotContain(run.Sent, fun r -> r.Method = Patch || r.Url.Contains "/git/refs")

    let o3 (create: string -> LiveRun.T) =
        let run, others = sequence create "persist013-o3"
        let head = LiveRun.head run
        for o in others do
            Assert.Equal(Ok true, Scenarios.reachable run.Provider o)
        Assert.Equal(Ok true, Scenarios.reachable run.Provider head)
        LiveRun.record run "PERSIST-013-O3" (head :: others)

    let o4 (create: string -> LiveRun.T) =
        let run, others = sequence create "persist013-o4"
        let saves = run.Events |> List.choose (function SaveInput.ReadBackMatched c -> Some c.CommitOid | _ -> None)
        for c in saves @ others do
            Assert.Equal(Ok true, Scenarios.reachable run.Provider c)
        LiveRun.record run "PERSIST-013-O4" (saves @ others)

    [<Fact>]
    let ``PERSIST-013-O1`` () = o1 LiveRun.create

    [<Fact>]
    let ``PERSIST-013-O2`` () = o2 LiveRun.create

    [<Fact>]
    let ``PERSIST-013-O3`` () = o3 LiveRun.create

    [<Fact>]
    let ``PERSIST-013-O4`` () = o4 LiveRun.create
