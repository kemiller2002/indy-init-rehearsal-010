// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// PERSIST-043 (live): commits to another investigation (one by the same tab) and to a file outside the investigations
// advance the branch before the tab saves; the save is confirmed without user action within three rebases.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PERSIST043Tests =
    type private Outcome =
        { Run: LiveRun.T
          Read: CommitOid
          SameTab: LiveRun.T
          Outside: string * CommitOid
          OtherInvestigation: LiveRun.T
          SentBefore: int }

    /// The tab reads its investigation; then the same tab saves another investigation, another tab saves a third
    /// investigation, and someone commits a file outside the investigations; then the tab saves.
    let private scenario (p: GitHubProvider) (label: string) =
        let run = LiveRun.createOn p (label + "-a")
        LiveRun.edit run "saved after unrelated commits" |> ignore
        let read = run.Session.BaseHead
        let sameTab = LiveRun.createWith run.Machine p (label + "-b")
        let otherTab = LiveRun.createOn p (label + "-c")
        let outsidePath = sprintf "notes/%s-%s.json" label (Guid.NewGuid().ToString("N").Substring(0, 8))
        let outside = Probes.otherWriter p outsidePath "{\"outside\":true}"
        let sentBefore = run.Sent.Length
        LiveRun.input run SaveInput.SaveRequested |> ignore
        { Run = run; Read = read; SameTab = sameTab; Outside = (outsidePath, outside); OtherInvestigation = otherTab; SentBefore = sentBefore }

    let private saveMutations (o: Outcome) = LiveRun.mutations { o.Run with Sent = o.Run.Sent |> List.skip o.SentBefore }

    /// O1: commits to another investigation and to a file outside the investigations advance the branch first.
    let o1 (mk: string -> GitHubProvider) =
        let p = mk "persist043-o1"
        let o = scenario p "persist043-o1"
        let since = LiveRun.commitsSince o.Run o.Read |> List.map fst
        for c in [ o.SameTab.Session.BaseHead; o.OtherInvestigation.Session.BaseHead; snd o.Outside ] do
            Assert.Contains((let (CommitOid x) = c in x), since)
        Assert.Equal(Some(CommitOnBranch o.Read), (saveMutations o).Head |> GitHubRequests.operationOf)
        Assert.Contains(LiveRun.prefix o.OtherInvestigation + "/events.ndjson", LiveRun.changedPaths o.Run o.OtherInvestigation.Session.BaseHead)
        Assert.Equal<string list>([ fst o.Outside ], LiveRun.changedPaths o.Run (snd o.Outside))

    /// O2: one of those commits is made by the same tab (its own machine and tab memory).
    let o2 (mk: string -> GitHubProvider) =
        let p = mk "persist043-o2"
        let o = scenario p "persist043-o2"
        Assert.True(obj.ReferenceEquals(o.Run.Machine, o.SameTab.Machine))
        Assert.False(obj.ReferenceEquals(o.Run.Machine, o.OtherInvestigation.Machine))
        Assert.Equal("Saved", MachineKit.stateName o.SameTab.Session.State)
        Assert.Contains(LiveRun.prefix o.SameTab + "/events.ndjson", LiveRun.changedPaths o.Run o.SameTab.Session.BaseHead)

    /// O3: confirmed without user action within at most three rebase attempts; the unrelated commits stay on the
    /// branch unchanged.
    let o3 (mk: string -> GitHubProvider) =
        let p = mk "persist043-o3"
        let o = scenario p "persist043-o3"
        Assert.Equal("Saved", MachineKit.stateName o.Run.Session.State)
        // Without user action: after the one save request, no Check again and no conflict resolution was needed.
        let afterRequest = o.Run.Events |> List.rev |> List.takeWhile (function SaveInput.Edit _ -> false | _ -> true) |> List.rev
        Assert.Equal(1, afterRequest |> List.filter ((=) SaveInput.SaveRequested) |> List.length)
        Assert.DoesNotContain(afterRequest, fun e -> match e with SaveInput.CheckAgain | SaveInput.ConflictResolvedReapplied _ | SaveInput.ConflictResolvedDiscarded _ -> true | _ -> false)
        let mutations = saveMutations o
        Assert.InRange(mutations.Length, 1, 4)
        let head = o.Run.Session.BaseHead
        for run in [ o.SameTab; o.OtherInvestigation ] do
            Assert.Equal(Ok true, Scenarios.reachable p run.Session.BaseHead)
            // Each other investigation still opens, unchanged, at the head after the save.
            match LiveRun.openInNewTab run with
            | Ok v ->
                Assert.Equal(head, v.ReadAt)
                Assert.Equal(run.Id, v.Investigation.Id)
                Assert.Equal(Causa.Domain.AggregateVersion 1L, v.Investigation.Version)
            | Error f -> failwithf "%A" f
        Assert.Equal(Some "{\"outside\":true}"B, Scenarios.readFile p head (fst o.Outside) |> Probes.ok "outside file")

    /// O4: the scenario passes against the fake and live.
    let o4 (mk: string -> GitHubProvider) =
        let fixture, _ = FixtureData.fixtureProvider []
        for p in [ fixture; mk "persist043-o4" ] do
            let o = scenario p "persist043-o4"
            Assert.Equal("Saved", MachineKit.stateName o.Run.Session.State)
            Assert.InRange((saveMutations o).Length, 2, 4)

    /// O5: PERSIST-009 (an unrelated stale head is rebased without asking: StaleHeadUnrelated, never a conflict) and
    /// PERSIST-013 (each save writes only under its own investigation's prefix).
    let o5 (mk: string -> GitHubProvider) =
        let p = mk "persist043-o5"
        let o = scenario p "persist043-o5"
        Assert.Contains(o.Run.Events, fun e -> match e with SaveInput.StaleHeadUnrelated _ -> true | _ -> false)
        Assert.DoesNotContain(o.Run.Events, fun e -> match e with SaveInput.StaleHeadSameInvestigation _ -> true | _ -> false)
        Assert.All(LiveRun.changedPaths o.Run o.Run.Session.BaseHead, fun path -> Assert.StartsWith(LiveRun.prefix o.Run + "/", path))

    [<Fact>]
    let ``PERSIST-043-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PERSIST-043-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PERSIST-043-O3`` () = o3 PfKit.live

    [<Fact>]
    let ``PERSIST-043-O4`` () = o4 PfKit.live

    [<Fact>]
    let ``PERSIST-043-O5`` () = o5 PfKit.live
