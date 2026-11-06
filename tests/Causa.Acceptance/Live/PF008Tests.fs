// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-008 (live): provider-failure fixtures, checked against GitHub's real answers wherever GitHub can be made to give them.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF008Tests =
    let private kind = Scenarios.kind

    let private withFault (fault: FakeFault) (f: GitHubProvider -> 'a) =
        let p, running = FixtureData.fixtureProvider []
        running.Update(StatefulFake.injectFault NextRequest fault)
        f p

    /// A transport that loses the answer to the n-th request (1-based) it carries: a partial transport failure.
    let private losingNth (n: int) (p: GitHubProvider) =
        let count = ref 0
        PfKit.through (fun r outcome -> count.Value <- count.Value + 1; if count.Value = n then NotAnswered(r.Id, LostAfterSending) else outcome) p

    /// A transport whose successful answers arrive corrupted (an HTML error page in place of JSON).
    let private corrupting (p: GitHubProvider) =
        PfKit.through (fun _ outcome -> match outcome with Answered(id, 200, h, _) -> Answered(id, 200, h, "<!DOCTYPE html><title>Unicorn</title>") | other -> other) p

    let private missingBranch (p: GitHubProvider) =
        { p with Connection = { p.Connection with Repository = { p.Connection.Repository with Branch = "causa-missing-" + Guid.NewGuid().ToString("N") } } }

    let private seededListing (p: GitHubProvider) =
        FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        p

    /// O1: every failure class is modelled by a fixture and classified to its IF41-ERRORS case; where GitHub can give
    /// the failure (bad credential, not found, missing branch 404, 422, timeout, offline, malformed and lost answers on
    /// real exchanges), the fixture's classification equals the classification of GitHub's own answer.
    let o1 (mk: string -> GitHubProvider) =
        let real = mk "pf008-o1"
        let fixture, _ = FixtureData.fixtureProvider []
        // Classes GitHub gives on demand: the fixture and the real answer must classify the same.
        let both: (string * (GitHubProvider -> string) * string) list =
            [ "authorization failure", (fun p -> kind (Probes.authFailure p)), "AuthenticationFailed"
              "not found", (fun p -> kind (Probes.notFound p (Probes.investigationPrefix "pf008"))), "NotFound"
              "missing branch (404 on the ref)", (fun p -> kind (Scenarios.readHead (missingBranch p))), "NotFound"
              "missing branch (save to it)",
              (fun p ->
                  let head = Scenarios.readHead p |> Probes.ok "head"
                  kind (Scenarios.commit (missingBranch p) (Scenarios.commitRequest head [ Probes.investigationPrefix "pf008" + "/x.json", "{}" ] (Probes.marker "missing")) |> snd)),
              "NotFound"
              "missing branch (HTTP 422: a ref at a commit that does not exist)",
              (fun p -> kind (Scenarios.send p CreateBranch (GitHubRequests.createBranch (Scenarios.nextId ()) p.Connection ("causa-pf008-" + Guid.NewGuid().ToString("N")) (CommitOid(String('0', 40)))) |> snd)),
              "ValidationFailed"
              "offline (the host cannot be reached)", (fun p -> let _, r, _, _ = Probes.networkBefore p (Probes.investigationPrefix "pf008") in kind r), "NetworkUnavailable.FailedBeforeSending"
              "malformed response", (fun p -> kind (Scenarios.readHead (corrupting p))), "Malformed"
              "partial transport failure (the catalog manifest answer is lost)",
              (fun p -> match ProviderPort.list (losingNth 3 (seededListing p)) ActiveCatalog with Error(ProviderFailed e) -> kind (Error e: Result<unit, ProviderError>) | other -> sprintf "%A" other),
              "NetworkUnavailable.LostAfterSending" ]
        for name, provoke, expected in both do
            let f = provoke fixture
            let r = provoke real
            Assert.True((f = expected), sprintf "fixture %s: %s" name f)
            Assert.True((r = f), sprintf "GitHub %s: %s, the fixture says %s" name r f)
        // Timeout: GitHub not answering within the time allowed, and the fixture's timeout, classify alike.
        let realTimeout = { real with Execute = LiveGitHub.executeWithin (TimeSpan.FromMilliseconds 1.0) }
        let timedOut = if real.Mode = LiveProvider then kind (Scenarios.readHead realTimeout) else withFault TimeOutBeforeApplying (fun p -> kind (Scenarios.readHead p))
        Assert.Equal("NetworkUnavailable.TimedOut", timedOut)
        Assert.Equal(timedOut, withFault TimeOutBeforeApplying (fun p -> kind (Scenarios.readHead p)))
        // Classes GitHub cannot be made to give on demand are modelled from its recorded forms.
        Assert.Equal("PermissionDenied", withFault (AnswerWith(403, Recordings.standardHeaders 4999, Recordings.resourceNotAccessible)) (fun p -> kind (Scenarios.readHead p)))
        Assert.Equal("RateLimited.Primary", withFault (AnswerWith(403, Recordings.standardHeaders 0, Recordings.primaryLimitMessage)) (fun p -> kind (Scenarios.readHead p)))
        Assert.Equal("RateLimited.Secondary", withFault (AnswerWith(403, ("retry-after", "60") :: Recordings.standardHeaders 4990, Recordings.secondaryLimitMessage)) (fun p -> kind (Scenarios.readHead p)))
        // Retry: a load that timed out is read again with a new request identity, and then verifies (T-L08).
        for p in [ fixture; real ] do
            let p = seededListing p
            let listing = ProviderPort.list p ActiveCatalog |> Result.defaultWith (fun f -> failwith (ProviderPort.failureCode f))
            let r = listing.Investigations.Head.Ref
            let machine = SaveMachines.create SaveMachines.support
            let first, effects = machine.StepLoad (ProviderPort.context p) NotLoaded (LoadInput.OpenRequested r) |> Result.defaultWith (fun f -> failwith f.Reason)
            let request = (SaveKit.requests effects).Head
            let failed =
                match machine.InterpretLoad (ProviderPort.context p) first (NotAnswered(request.Id, TimedOut)) with
                | LoadEvent input -> machine.StepLoad (ProviderPort.context p) first input |> Result.defaultWith (fun f -> failwith f.Reason) |> fst
                | other -> failwithf "%A" other
            Assert.Equal(LoadFailed(ProviderFailed(NetworkUnavailable TimedOut)), failed)
            let again, effects2 = machine.StepLoad (ProviderPort.context p) failed (LoadInput.OpenRequested r) |> Result.defaultWith (fun f -> failwith f.Reason)
            let retry = (SaveKit.requests effects2).Head
            Assert.NotEqual(request.Id, retry.Id)
            // The retry's answer, and any follow-up read of the same load (the catalog manifest), executed in turn.
            let rec settle state outcome =
                match machine.InterpretLoad (ProviderPort.context p) state outcome with
                | LoadNextRequest(next, request) -> settle next (p.Execute request)
                | other -> other
            match settle again (p.Execute retry) with
            | LoadEvent(LoadInput.LoadVerified v) -> Assert.Equal(r.Id, v.Investigation.Id)
            | other -> failwithf "the retry did not verify: %A" other

    /// O2: provider errors and domain validation errors are different outcomes: a provider refusal is
    /// LoadFailure.ProviderFailed, a file set the engine refuses is LoadFailure.Unverifiable, never the other.
    let o2 (mk: string -> GitHubProvider) =
        let real = mk "pf008-o2"
        let cases = FixtureData.edgeCases ()
        let missing = cases |> List.find (fun c -> c.Name = "missing investigation")
        let invalid = cases |> List.find (fun c -> c.Name.StartsWith "invalid schema")
        match FixtureData.runEdgeCase real "pf008" missing with
        | Error(ProviderFailed(NotFound _)) -> ()
        | other -> failwithf "a missing investigation is a provider NotFound, not %A" (other |> Result.map ignore)
        match FixtureData.runEdgeCase real "pf008" invalid with
        | Error(Unverifiable failures) -> Assert.NotEmpty(failures)
        | other -> failwithf "an invalid file set is a domain validation failure, not %A" (other |> Result.map ignore)
        // A provider failure during a load never turns into a validation failure, and the reverse.
        let bad = { real with Connection = { real.Connection with Credential = Credential.create "invalid-credential-for-conformance" } }
        match ProviderPort.openRef bad { Address = FixtureData.edgeWorkspace "pf008-auth"; Id = InvestigationId missing.Id } with
        | Error(ProviderFailed AuthenticationFailed) -> ()
        | other -> failwithf "a refused credential is a provider failure, not %A" (other |> Result.map ignore)

    [<Fact>]
    let ``PF-008-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-008-O2`` () = o2 PfKit.live
