// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// PERSIST-042 (live): another writer advances the branch after the tab read it; GitHub refuses the stale mutation,
// it is classified as stale and nothing else, nothing of it is written; every other failure has its own class.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PERSIST042Tests =
    /// O1: another writer advances the branch after the tab read it.
    let o1 (mk: string -> GitHubProvider) =
        let p = mk "persist042-o1"
        let probe = Probes.staleHead p (Probes.investigationPrefix "persist042")
        Assert.NotEqual(probe.Base, probe.OtherHead)
        Assert.Equal(Ok true, Scenarios.reachable p probe.Base)
        Assert.Equal(Ok true, Scenarios.reachable p probe.OtherHead)
        Assert.Equal(probe.Base, probe.Change.ExpectedHead)

    /// O2: GitHub refuses the tab's mutation at the stale head; it is StaleHead, not a network, permission, rate-limit
    /// or validation failure; nothing of the tab's change was written.
    let o2 (mk: string -> GitHubProvider) =
        let p = mk "persist042-o2"
        let probe = Probes.staleHead p (Probes.investigationPrefix "persist042")
        match probe.Result with
        | Error(StaleHead _) -> ()
        | other -> failwithf "the stale mutation was classified %A" other
        for wrong in [ "NetworkUnavailable"; "PermissionDenied"; "RateLimited"; "ValidationFailed" ] do
            Assert.False((Scenarios.kind probe.Result).StartsWith wrong)
        Assert.Equal(None, probe.MineAtHeadAfter)
        Assert.Equal(probe.OtherHead, probe.HeadAfter)
        // In the application: the session sees the stale head and saves only after it, with one commit of its change.
        let run = LiveRun.createOn p "persist042-o2"
        LiveRun.edit run "stale then saved" |> ignore
        let other = Probes.otherWriter p "unrelated/persist042.json" "{}"
        LiveRun.input run SaveInput.SaveRequested |> ignore
        Assert.Contains(SaveInput.StaleHeadUnrelated other, run.Events)
        Assert.DoesNotContain(run.Events, fun e -> match e with SaveInput.ProviderRefused _ | SaveInput.AnswerLost | SaveInput.FailedBeforeSending -> true | _ -> false)
        Assert.Equal(1, (LiveRun.commitsSince run other).Length)

    /// The mutation sent through a transport that answers with the given form instead of sending it (an injected failure).
    let private injected (p: GitHubProvider) (form: HttpOutcome option) =
        let head = Scenarios.readHead p |> Probes.ok "head"
        let change = Scenarios.commitRequest head [ Probes.investigationPrefix "persist042" + "/x.json", "{}" ] (Probes.marker "inject")
        let transport = { p with Execute = fun r -> match form with Some(Answered(_, s, h, b)) -> Answered(r.Id, s, h, b) | Some(NotAnswered(_, f)) -> NotAnswered(r.Id, f) | None -> p.Execute r }
        let _, result = Scenarios.commit transport change
        let after = Scenarios.readHead p |> Probes.ok "head after"
        result, head, after

    /// O3: each other failure, injected into the same mutation, gets its own classification, and none is StaleHead.
    let o3 (mk: string -> GitHubProvider) =
        let p = mk "persist042-o3"
        let cases =
            [ "NetworkUnavailable.FailedBeforeSending", Some(NotAnswered(RequestId 0L, FailedBeforeSending))
              "PermissionDenied", Some(Answered(RequestId 0L, 200, Recordings.standardHeaders 4999, Recordings.graphQlForbidden))
              "RateLimited.Secondary", Some(Answered(RequestId 0L, 403, ("retry-after", "60") :: Recordings.standardHeaders 4990, Recordings.secondaryLimitMessage))
              "RateLimited.Primary", Some(Answered(RequestId 0L, 403, Recordings.standardHeaders 0, Recordings.primaryLimitMessage))
              "ValidationFailed", Some(Answered(RequestId 0L, 200, Recordings.standardHeaders 4999, Recordings.graphQlInputInvalid)) ]
        for expected, form in cases do
            let result, head, after = injected p form
            Assert.True((Scenarios.kind result = expected), sprintf "%s classified %s" expected (Scenarios.kind result))
            Assert.Equal(head, after)
        // The real forms GitHub gives for some of them, against the live repository.
        let _, offline, h0, h1 = Probes.networkBefore p (Probes.investigationPrefix "persist042")
        Assert.Equal("NetworkUnavailable.FailedBeforeSending", Scenarios.kind offline)
        Assert.Equal(h0, h1)
        Assert.Equal("ValidationFailed", Scenarios.kind (Probes.validationFailure p))
        Assert.Equal("AuthenticationFailed", Scenarios.kind (Probes.authFailure p))

    /// O4: the whole scenario passes on this candidate, recorded with its source SHA.
    let o4 (mk: string -> GitHubProvider) =
        let p = mk "persist042-o4"
        let probe = Probes.staleHead p (Probes.investigationPrefix "persist042")
        Assert.Equal("StaleHead", Scenarios.kind probe.Result)
        Assert.Equal(Some probe.Change.Additions.Head.Contents, probe.ResentBytes)
        let line = ProviderEvidence.record p "PERSIST-042-O4" [ probe.OtherHead; probe.Resent ] [ "sourceSha", PfKit.sourceSha () ]
        Assert.Contains("\"sourceSha\"", line)

    /// O5: PERSIST-008: the classification reads the answer's typed content, not the status alone: the stale answer is
    /// HTTP 200, like a successful commit, and only its GraphQL error says it is stale.
    let o5 (mk: string -> GitHubProvider) =
        let p = mk "persist042-o5"
        let probe = Probes.staleHead p (Probes.investigationPrefix "persist042")
        match probe.Outcome with
        | Answered(_, status, _, _) -> Assert.Equal(200, status)
        | other -> failwithf "%A" other
        let types = Scenarios.errorTypes probe.Outcome
        Assert.True(types |> List.exists (fun t -> t = "STALE_DATA" || t = "FORBIDDEN" || t = "<none>"), sprintf "%A" types)
        Assert.Equal("StaleHead", Scenarios.kind probe.Result)
        Assert.Equal("Ok", Scenarios.kind (Classify.answer (CommitOnBranch probe.Base) (Answered(RequestId 1L, 200, Recordings.standardHeaders 4999, Recordings.commitCreated (String('c', 40))))))

    [<Fact>]
    let ``PERSIST-042-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PERSIST-042-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PERSIST-042-O3`` () = o3 PfKit.live

    [<Fact>]
    let ``PERSIST-042-O4`` () = o4 PfKit.live

    [<Fact>]
    let ``PERSIST-042-O5`` () = o5 PfKit.live
