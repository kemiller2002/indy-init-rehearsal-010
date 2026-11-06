// Owner: persistence.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

/// PERSIST-008 (PAT-03): stale-head detection and typed classification, against the real test repository and the
/// stateful fake. Every test needs CAUSA_GITHUB_TOKEN; without it the test fails as BLOCKED (never passes).
module PERSIST008Tests =

    let private live (label: string) = LiveGitHub.freshBranch (LiveGitHub.require ()) label

    let private fake () = Scenarios.fakeProvider "causa-acceptance" [ "README.md", "synthetic"B ]

    let private isStale result =
        match result with
        | Error(StaleHead _) -> true
        | _ -> false

    let private checkStale (probe: Probes.StaleProbe) =
        // GitHub refused the mutation for the stale head, and the application classifies exactly that.
        match probe.Result with
        | Error(StaleHead(expected, actual)) ->
            Assert.Equal(probe.Base, expected)
            actual |> Option.iter (fun a -> Assert.Equal(probe.OtherHead, a))
        | other -> failwithf "expected StaleHead, got %A" other
        // Nothing was written: the head is the other writer's commit and the tab's file is not there.
        Assert.Equal(probe.OtherHead, probe.HeadAfter)
        Assert.Equal(None, probe.MineAtHeadAfter)
        // The change stayed unsaved in memory: the same bytes save at the new head and read back equal.
        Assert.Equal(Some(probe.Change.Additions.Head.Contents), probe.ResentBytes)

    [<Fact>]
    let ``PERSIST-008-O1`` () =
        let p = live "persist008-o1"
        let probe = Probes.staleHead p (Probes.investigationPrefix "live")
        checkStale probe
        ProviderEvidence.record p "PERSIST-008-O1" [ probe.OtherHead; probe.Resent ] [ "staleErrorTypes", String.Join(",", Scenarios.errorTypes probe.Outcome) ]
        |> ignore

    [<Fact>]
    let ``PERSIST-008-O2`` () =
        let p = live "persist008-o2"
        let prefix = Probes.investigationPrefix "live"
        let stale = (Probes.staleHead p prefix).Result
        let auth = Probes.authFailure p
        let validation = Probes.validationFailure p
        let missing = Probes.notFound p prefix
        let _, network, _, _ = Probes.networkBefore p prefix
        Assert.True(isStale stale, sprintf "stale head: %A" stale)
        Assert.Equal(Error AuthenticationFailed, auth)
        Assert.True((match validation with Error(ValidationFailed _) -> true | _ -> false), sprintf "validation: %A" validation)
        Assert.True((match missing with Error(NotFound _) -> true | _ -> false), sprintf "not found: %A" missing)
        Assert.Equal(Error(NetworkUnavailable FailedBeforeSending), network)
        // Forms GitHub cannot be made to give safely on the test repository (rate limits, a read-only credential) are
        // classified from their recorded content; each lands in its own class and none is a stale head.
        let recorded (form: string) =
            let r = Recordings.all |> List.find (fun x -> x.Form = form)
            Classify.answer r.Operation r.Outcome
        let secondary = recorded "rest-403-secondary-message-only"
        let primary = recorded "rest-403-primary"
        let permission = recorded "graphql-forbidden-not-accessible"
        let kinds =
            [ Scenarios.kind stale
              Scenarios.kind auth
              Scenarios.kind validation
              Scenarios.kind missing
              Scenarios.kind network
              Scenarios.kind secondary
              Scenarios.kind primary
              Scenarios.kind permission ]
        Assert.Equal(kinds.Length, (List.distinct kinds).Length)
        Assert.Equal<string list>(
            [ "StaleHead"; "AuthenticationFailed"; "ValidationFailed"; "NotFound"; "NetworkUnavailable.FailedBeforeSending"
              "RateLimited.Secondary"; "RateLimited.Primary"; "PermissionDenied" ],
            kinds)
        ProviderEvidence.record p "PERSIST-008-O2" [] (List.zip [ "stale"; "auth"; "validation"; "notFound"; "network"; "secondary"; "primary"; "permission" ] kinds)
        |> ignore

    [<Fact>]
    let ``PERSIST-008-O3`` () =
        let p = live "persist008-o3"
        let prefix = Probes.investigationPrefix "live"
        // Before sending: nothing reaches GitHub, and it is a network failure, not a stale head or a conflict.
        let _, before, head, after = Probes.networkBefore p prefix
        Assert.Equal(Error(NetworkUnavailable FailedBeforeSending), before)
        Assert.Equal(head, after)
        // After sending: the answer is lost (a timeout shorter than GitHub's answer). The outcome is unknown, never
        // stale; reconciliation then finds either this save's marker at the head or an unchanged head.
        let lost = Probes.lostAnswer p (LiveGitHub.executeWithin (TimeSpan.FromMilliseconds 1.0)) prefix
        match lost.Classified with
        | Error(NetworkUnavailable(TimedOut | LostAfterSending)) -> ()
        | other -> failwithf "a lost answer was classified as %A" other
        Assert.False(isStale lost.Classified)
        Assert.True(lost.MarkerAtHead || lost.HeadAfter = lost.Expected, "reconciliation found neither the marker nor an unchanged head")
        ProviderEvidence.record p "PERSIST-008-O3" [ lost.HeadAfter ] [ "lostOutcome", sprintf "%A" lost.Classified; "applied", string lost.MarkerAtHead ]
        |> ignore

    [<Fact>]
    let ``PERSIST-008-O4`` () =
        // PAT-03 in both modes: each classification is the same from the stateful fake and from GitHub.
        let livePrefix = Probes.investigationPrefix "live"
        let p = live "persist008-o4"
        let f, _ = fake ()
        let fakePrefix = Probes.investigationPrefix "fake"
        let run (provider: GitHubProvider) prefix =
            let stale = Probes.staleHead provider prefix
            checkStale stale
            let _, network, _, _ = Probes.networkBefore provider prefix
            [ "stale", Scenarios.kind stale.Result
              "auth", Scenarios.kind (Probes.authFailure provider)
              "validation", Scenarios.kind (Probes.validationFailure provider)
              "notFound", Scenarios.kind (Probes.notFound provider prefix)
              "network", Scenarios.kind network ],
            stale
        let liveKinds, liveStale = run p livePrefix
        let fakeKinds, fakeStale = run f fakePrefix
        Assert.Equal<(string * string) list>(fakeKinds, liveKinds)
        Assert.Equal<(string * string) list>(
            [ "stale", "StaleHead"; "auth", "AuthenticationFailed"; "validation", "ValidationFailed"; "notFound", "NotFound"
              "network", "NetworkUnavailable.FailedBeforeSending" ],
            liveKinds)
        // The fake answers the stale head in GitHub's shape (PF-013: fake/real divergence is detected here).
        let shape outcome =
            match outcome with
            | Answered(_, status, _, body) -> status, Scenarios.shape body
            | NotAnswered _ -> 0, []
        Assert.Equal(shape liveStale.Outcome, shape fakeStale.Outcome)
        let liveTypes = Scenarios.errorTypes liveStale.Outcome
        Assert.NotEmpty(liveTypes)
        Assert.All(liveTypes, fun t -> Assert.Contains(t, [ "STALE_DATA"; "FORBIDDEN" ]))
        ProviderEvidence.record p "PERSIST-008-O4" [ liveStale.OtherHead; liveStale.Resent ] liveKinds |> ignore
