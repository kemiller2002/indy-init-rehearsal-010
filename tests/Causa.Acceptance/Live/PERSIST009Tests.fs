// Owner: persistence. PERSIST-009 (live): an unrelated stale head is rebased without asking the user.
namespace Causa.Acceptance.Live

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST009Tests =
    /// Another writer commits an unrelated file after this tab read the branch; the tab then saves.
    let private rebased (create: string -> LiveRun.T) (label: string) =
        let run = create label
        let baseHead = run.Session.BaseHead
        LiveRun.edit run "rebased change" |> ignore
        let other = Probes.otherWriter run.Provider (sprintf "unrelated/%s.json" label) "{}"
        let sentBefore = run.Sent.Length
        LiveRun.input run SaveInput.SaveRequested |> ignore
        { run with Sent = run.Sent |> List.skip sentBefore }, baseHead, other

    let o1 (create: string -> LiveRun.T) =
        let run, _, other = rebased create "persist009-o1"
        // After the stale head: a fresh read of the head, then the investigation's files at that head.
        let ops = run.Sent |> List.choose GitHubRequests.operationOf
        let afterFirst = ops |> List.skipWhile (function CommitOnBranch _ -> false | _ -> true) |> List.tail
        Assert.Equal(2, ops |> List.filter (function CommitOnBranch _ -> true | _ -> false) |> List.length)
        match afterFirst with
        | ReadBranchHead :: ReadInvestigationAt rev :: _ -> Assert.Equal((let (CommitOid o) = other in o), rev)
        | x -> failwithf "%A" x

    let o2 (create: string -> LiveRun.T) =
        let run, _, other = rebased create "persist009-o2"
        Assert.Contains(SaveInput.StaleHeadUnrelated other, run.Events)
        Assert.DoesNotContain(run.Events, fun e -> match e with SaveInput.StaleHeadSameInvestigation _ -> true | _ -> false)

    let o3 (create: string -> LiveRun.T) =
        let run, baseHead, other = rebased create "persist009-o3"
        match LiveRun.mutations run with
        | [ first; second ] ->
            Assert.Equal(Some(CommitOnBranch baseHead), GitHubRequests.operationOf first)
            Assert.Equal(Some(CommitOnBranch other), GitHubRequests.operationOf second)
            // The same events, manifest and snapshot are sent again.
            let files (r: HttpRequest) =
                use doc = System.Text.Json.JsonDocument.Parse(r.Body.Value)
                doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("fileChanges").GetRawText()
            Assert.Equal(files first, files second)
        | _ -> failwith "two mutations expected"
        Assert.Equal("Saved", MachineKit.stateName run.Session.State)

    let o4 (create: string -> LiveRun.T) =
        let run = create "persist009-o4"
        LiveRun.edit run "stays saving" |> ignore
        let other = Probes.otherWriter run.Provider "unrelated/o4.json" "{}"
        // Step by step: after the stale answer, the head read and the file comparison, the state is still Saving.
        let next, effects = SaveKit.stepOk run.Machine run.Clock run.Session SaveInput.SaveRequested
        let rec walk (s: InvestigationSession) (requests: HttpRequest list) states =
            match requests with
            | [] -> s, List.rev states
            | r :: _ ->
                match run.Machine.Interpret (SaveKit.context run.Clock) s (run.Provider.Execute r) with
                | Interpretation.NextRequest(n, req) ->
                    run.Clock.Next <- run.Clock.Next + 1L
                    walk n [ req ] (MachineKit.stateName n.State :: states)
                | MachineEvent input ->
                    let n, e = SaveKit.stepOk run.Machine run.Clock s input
                    walk n (SaveKit.requests e) (MachineKit.stateName n.State :: states)
        let final, states = walk next (SaveKit.requests effects) []
        Assert.Equal("Saved", MachineKit.stateName final.State)
        Assert.All(states |> List.take (states.Length - 1), fun s -> Assert.Equal("Saving", s))
        ignore other

    let o5 (create: string -> LiveRun.T) =
        let run, _, other = rebased create "persist009-o5"
        Assert.Equal("Saved", MachineKit.stateName run.Session.State)
        // The unrelated commit stays on the branch, as the parent of the rebased save.
        Assert.Equal(Ok true, Scenarios.reachable run.Provider other)
        let commits = LiveRun.commitsSince run other
        Assert.Equal(1, commits.Length)
        LiveRun.record run "PERSIST-009-O5" [ other; run.Session.BaseHead ]

    [<Fact>]
    let ``PERSIST-009-O1`` () = o1 LiveRun.create

    [<Fact>]
    let ``PERSIST-009-O2`` () = o2 LiveRun.create

    [<Fact>]
    let ``PERSIST-009-O3`` () = o3 LiveRun.create

    [<Fact>]
    let ``PERSIST-009-O4`` () = o4 LiveRun.create

    [<Fact>]
    let ``PERSIST-009-O5`` () = o5 LiveRun.create
