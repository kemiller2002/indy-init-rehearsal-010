// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-011 (live): four independent verification levels: domain, UI, provider conformance, live integration.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF011Tests =
    let private suitesOf (layer: Layers.Layer) (suites: Layers.Suite list) = suites |> List.filter (fun s -> Layers.ofSuite s.Id = Some layer)

    /// O1: the domain level is deterministic F# validation, reconstruction and transition checking with no external
    /// dependency: replaying the frozen fixtures twice gives identical results, the domain assembly has no network
    /// stack, and the same bytes fetched from GitHub reconstruct identically.
    let o1 (mk: string -> GitHubProvider) =
        let replay () =
            FixtureData.frozenEntries ()
            |> List.map (fun e ->
                let id = Canonical.str "investigationId" e |> Option.get
                match StorageReader.verify Kernel.domain (Some(InvestigationId(Guid id))) (WorkspaceAddress.catalog (Workspace "fixtures/v1.0.0")) (CommitOid "domain") (FixtureData.frozenFiles id) with
                | Ok v -> v.Investigation
                | Error f -> failwithf "%A" f)
        let first = replay ()
        Assert.Equal<Investigation list>(first, replay ())
        Assert.DoesNotContain("System.Net.Http", typeof<DomainKernel>.Assembly.GetReferencedAssemblies() |> Array.map (fun a -> string a.Name))
        use plan = Layers.plan ()
        Assert.All(suitesOf Layers.Domain (Layers.suites plan), fun s -> Assert.Equal("ci", s.Environment))
        let real = mk "pf011-o1"
        FixtureData.seed real (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        match ProviderPort.catalog real with
        | Ok(_, rs) -> Assert.Equal<Investigation list>(first, rs |> List.map (fun r -> match r.Result with Ok v -> v.Investigation | Error f -> failwith (ProviderPort.failureCode f)))
        | Error f -> failwith (ProviderPort.failureCode f)

    /// O2: the UI level runs browser tests on local fixtures only: its suites run in the offline browser environment,
    /// and no browser test outside the live suite names GitHub or a credential.
    let o2 (mk: string -> GitHubProvider) =
        let _ = mk "pf011-o2"
        use plan = Layers.plan ()
        let ui = suitesOf Layers.Ui (Layers.suites plan)
        Assert.NotEmpty(ui)
        Assert.All(ui, fun s -> Assert.Equal("ci-browser", s.Environment); Assert.StartsWith("npx playwright test", s.Command))
        Assert.NotEmpty(Layers.tests plan |> List.filter (fun t -> ui |> List.exists (fun s -> s.Id = t.Suite)))
        for path, text in PfKit.sources "tests/browser" [ ".js"; ".mjs"; ".ts" ] do
            if not (path.Replace('\\', '/').Contains "/live/") then
                for marker in [ "https://api.github.com"; LiveGitHub.TokenVariable ] do
                    Assert.False(text.Contains marker, sprintf "%s names %s" path marker)

    /// O3: provider conformance: the same contract scenarios run against the fixture and the GitHub adapter and agree;
    /// the recorded real answers the fixture is built from carry no credential.
    let o3 (mk: string -> GitHubProvider) =
        let real = mk "pf011-o3"
        let fixture, _ = FixtureData.fixtureProvider []
        let scenarios (p: GitHubProvider) =
            let prefix = Probes.investigationPrefix "pf011"
            [ Scenarios.kind (Scenarios.readHead p)
              Scenarios.kind (Probes.authFailure p)
              Scenarios.kind (Probes.notFound p prefix)
              Scenarios.kind (Probes.validationFailure p)
              Scenarios.kind (Probes.staleHead p prefix).Result ]
        let f = scenarios fixture
        Assert.Equal<string list>([ "Ok"; "AuthenticationFailed"; "NotFound"; "ValidationFailed"; "StaleHead" ], f)
        Assert.Equal<string list>(f, scenarios real)
        for r in Recordings.all do
            let text = sprintf "%A" r.Outcome
            for marker in [ "ghp_"; "gho_"; "ghu_"; "ghs_"; "ghr_"; "github_pat_"; "Bearer " ] do
                Assert.DoesNotContain(marker, text)

    /// O4: live integration: real GitHub requests discover the active catalog, transfer and reconstruct every listed
    /// investigation, match each expected state digest, and report a missing investigation as an error.
    let o4 (mk: string -> GitHubProvider) =
        let real = mk "pf011-o4"
        let commit = FixtureData.seed real (FixtureData.demonstrationCatalog ()) "Causa fixtures: demonstration-v1 catalog"
        match ProviderPort.catalog real with
        | Error f -> failwith (ProviderPort.failureCode f)
        | Ok(listing, rs) ->
            Assert.Equal(commit, listing.ReadAt)
            Assert.Equal(Workspace "workspaces/demo/demonstration-v1", listing.Address.Workspace)
            Assert.Equal(4, rs.Length)
            for r in rs do
                Assert.True(ProviderPort.digestMatches r, sprintf "%A does not reproduce its expected state" r.Listed.Title)
            let missing = ProviderPort.openRef real { rs.Head.Listed.Ref with Id = InvestigationId(Guid "0e7e0000-0000-4000-8000-0000000000b4") }
            Assert.Equal(Error "provider:NotFound", missing |> Result.map ignore |> Result.mapError ProviderPort.failureCode)
            ProviderEvidence.record real "PF-011-O4" [ commit ] [ "reconstructed", string rs.Length ] |> ignore

    /// O5: each level has its own independently executable command and CI outcome, and leaves evidence.
    let o5 (mk: string -> GitHubProvider) =
        let real = mk "pf011-o5"
        use plan = Layers.plan ()
        let suites = Layers.suites plan
        for layer in [ Layers.Domain; Layers.Ui; Layers.ProviderContract; Layers.LiveIntegration ] do
            let own = suitesOf layer suites
            Assert.True(not own.IsEmpty, sprintf "%A has no suite" layer)
            Assert.All(own, fun s -> Assert.False(String.IsNullOrWhiteSpace s.Command))
        // Each suite has its own tests, so each has its own outcome.
        let tests = Layers.tests plan
        Assert.All(suites |> List.filter (fun s -> Layers.ofSuite s.Id <> Some Layers.Process && s.Id <> "SU-BROWSER-LIVE"), fun s -> Assert.Contains(tests, fun t -> t.Suite = s.Id))
        let gate = IO.File.ReadAllText(IO.Path.Combine(Layers.repositoryRoot (), ".github", "workflows", "verification-gate.yml"))
        Assert.Contains("run-suites", gate)
        let line = ProviderEvidence.record real "PF-011-O5" [] [ "layer", "live-integration" ]
        Assert.Contains("\"test\":\"PF-011-O5\"", line)

    /// O6: a higher level never conceals a lower one: a test belongs to exactly one suite, the live suites select no
    /// deterministic test, and every live suite runs only after the deterministic suites it depends on.
    let o6 (mk: string -> GitHubProvider) =
        let _ = mk "pf011-o6"
        use plan = Layers.plan ()
        let suites = Layers.suites plan
        let tests = Layers.tests plan
        Assert.Equal(tests.Length, (tests |> List.map (fun t -> t.Id) |> List.distinct).Length)
        let live = suites |> List.find (fun s -> s.Id = "SU-DOTNET-LIVE")
        Assert.DoesNotContain("Deterministic", live.Command)
        Assert.All(tests |> List.filter (fun t -> t.Suite = "SU-DOTNET-LIVE"), fun t -> Assert.Contains(".Live.", t.Id))
        Assert.All(tests |> List.filter (fun t -> t.Id.Contains "Causa.Acceptance.Deterministic."), fun t -> Assert.Equal("SU-DOTNET-DETERMINISTIC", t.Suite))
        let ancestors = Layers.ancestors suites "SU-DOTNET-LIVE"
        Assert.Contains("SU-DOTNET-DETERMINISTIC", ancestors)
        Assert.Contains("SU-OWN-DOTNET", ancestors)

    [<Fact>]
    let ``PF-011-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-011-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-011-O3`` () = o3 PfKit.live

    [<Fact>]
    let ``PF-011-O4`` () = o4 PfKit.live

    [<Fact>]
    let ``PF-011-O5`` () = o5 PfKit.live

    [<Fact>]
    let ``PF-011-O6`` () = o6 PfKit.live
