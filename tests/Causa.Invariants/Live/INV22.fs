// Owner: persistence.
namespace Causa.Invariants.Live

open System
open System.Threading.Tasks
open Xunit
open Causa.Persistence
open Causa.TestSupport

/// INV-22 (live): GitHub's answers to concurrent writers are classified from their content; every stale-head refusal,
/// in whichever form GitHub sends it (STALE_DATA or the FORBIDDEN moved-head form, KD-022), is a stale head, never a
/// permission refusal or an unrecognised answer. Needs CAUSA_GITHUB_TOKEN; without it the test fails as BLOCKED.
module INV22 =

    let private staleForm (outcome: HttpOutcome) =
        match outcome with
        | Answered(_, _, _, body) ->
            Scenarios.errorTypes outcome |> List.contains "STALE_DATA"
            || body.Contains("but expected", StringComparison.Ordinal)
            || body.Contains("Expected branch to point to", StringComparison.Ordinal)
        | NotAnswered _ -> false

    /// Three commits sent at the same moment, all conditional on the same head.
    let private threeAtOnce (p: GitHubProvider) =
        let prefix = Probes.investigationPrefix (if p.Mode = LiveProvider then "live" else "fake")
        let head = Scenarios.readHead p |> Probes.ok "head"
        let changes =
            [ for i in 1..3 -> Scenarios.commitRequest head [ sprintf "%s-%d/events.ndjson" prefix i, sprintf "{\"tab\":%d}\n" i ] (Probes.marker (sprintf "tab%d" i)) ]
        let results =
            changes
            |> List.map (fun c -> Task.Run(fun () -> c, Scenarios.commit p c))
            |> List.map (fun t -> t.Result)
        head, results

    let private check (p: GitHubProvider) =
        let head, results = threeAtOnce p
        let successes = results |> List.choose (fun (_, (_, r)) -> match r with Ok c -> Some c | Error _ -> None)
        let refusals = results |> List.choose (fun (c, (o, r)) -> match r with Error e -> Some(c, o, e) | Ok _ -> None)
        // GitHub accepts at most one commit conditional on one head.
        Assert.True(successes.Length <= 1, sprintf "%d commits were accepted at one expected head" successes.Length)
        for (_, outcome, error) in refusals do
            match error with
            | StaleHead(expected, _) -> Assert.Equal(head, expected)
            | RateLimited _ -> Assert.False(staleForm outcome, "a stale-head answer was classified as a rate limit")
            | other -> failwithf "a concurrent writer's refusal was classified as %A (answer types %A)" other (Scenarios.errorTypes outcome)
            if staleForm outcome then
                Assert.True((match error with StaleHead _ -> true | _ -> false))
        // Every refused change is kept and saves at the new head; every writer's commit stays reachable.
        let resent =
            refusals
            |> List.filter (fun (_, _, e) -> match e with StaleHead _ -> true | _ -> false)
            |> List.map (fun (change, _, _) ->
                let fresh = Scenarios.readHead p |> Probes.ok "fresh head"
                Scenarios.commit p { change with ExpectedHead = fresh } |> snd |> Probes.ok "resend")
        let all = successes @ resent
        for commit in all do
            Assert.Equal(Ok true, Scenarios.reachable p commit)
        let forms = refusals |> List.map (fun (_, o, _) -> String.Join("+", Scenarios.errorTypes o))
        all, forms

    [<Fact>]
    let ThreeSimultaneousCommits () =
        let p = LiveGitHub.freshBranch (LiveGitHub.require ()) "inv22"
        let f, _ = Scenarios.fakeProvider "causa-acceptance" [ "README.md", "synthetic"B ]
        let fakeCommits, fakeForms = check f
        Assert.Equal(3, fakeCommits.Length)
        Assert.Equal<string list>([ "STALE_DATA"; "STALE_DATA" ], fakeForms)
        let liveCommits, liveForms = check p
        ProviderEvidence.record p "INV22.ThreeSimultaneousCommits" liveCommits [ "refusalForms", String.Join(";", liveForms) ] |> ignore
