// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-012 (live): fast local feedback: documented commands, offline CI without secrets, timings and failures by layer.
namespace Causa.Acceptance.Live

open System
open System.Diagnostics
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF012Tests =
    let private root () = Layers.repositoryRoot ()

    /// O1: README.md documents one command that builds and starts a populated fixture-backed UI and a separate command
    /// for the fast offline domain, UI and provider-contract checks (the plan's offline suites).
    let o1 (mk: string -> GitHubProvider) =
        let _ = mk "pf012-o1"
        let readme = IO.File.ReadAllText(IO.Path.Combine(root (), "README.md"))
        use plan = Layers.plan ()
        let suites = Layers.suites plan
        let offline = suites |> List.filter (fun s -> s.Id = "SU-OWN-DOTNET" || s.Id = "SU-DOTNET-DETERMINISTIC")
        Assert.Equal(2, offline.Length)
        Assert.True(
            readme.Contains "Causa.Acceptance.Deterministic." && readme.Contains "dotnet test",
            "README.md does not document the offline domain/UI/provider-contract command (PF-012-O1; integration owns README.md)"
        )
        let lines = readme.Split('\n')
        Assert.True(
            lines |> Array.exists (fun l -> l.Contains "fixture" && (l.Contains "npm run" || l.Contains "dotnet fsi" || l.Contains "node ")),
            "README.md documents no single command that builds and starts the populated fixture-backed UI (PF-012-O1; integration owns README.md)"
        )

    /// O2: CI runs the offline deterministic checks without secrets: the gate's development run selects the offline
    /// suites, whose environments hold no provider secret, and the build-and-test workflow reads no secret at all.
    let o2 (mk: string -> GitHubProvider) =
        let _ = mk "pf012-o2"
        let workflows = IO.Path.Combine(root (), ".github", "workflows")
        let gate = IO.File.ReadAllText(IO.Path.Combine(workflows, "verification-gate.yml"))
        let development = gate.Split('\n') |> Array.find (fun l -> l.Contains "MODE\" = \"development\" ]; then suites=")
        for suite in [ "SU-OWN-DOTNET"; "SU-DOTNET-DETERMINISTIC"; "SU-DOTNET-INVARIANTS" ] do
            Assert.Contains(suite, development)
        Assert.DoesNotContain("SU-DOTNET-LIVE", development)
        use plan = Layers.plan ()
        for s in Layers.suites plan do
            if [ "SU-OWN-DOTNET"; "SU-DOTNET-DETERMINISTIC"; "SU-DOTNET-INVARIANTS" ] |> List.contains s.Id then Assert.Equal("ci", s.Environment)
        let buildAndTest = IO.File.ReadAllText(IO.Path.Combine(workflows, "build-and-test.yml"))
        Assert.DoesNotContain("secrets.", buildAndTest)
        Assert.Contains("dotnet test", buildAndTest)

    /// O3: execution time and failures are recorded per layer (domain, provider contract, live integration), as a
    /// baseline: no budget is asserted before one has been measured.
    let o3 (mk: string -> GitHubProvider) =
        let real = mk "pf012-o3"
        let timed (f: unit -> int) =
            let w = Stopwatch.StartNew()
            let failures = try f () with _ -> -1
            w.ElapsedMilliseconds, failures
        let domain =
            timed (fun () ->
                FixtureData.frozenEntries ()
                |> List.filter (fun e ->
                    let id = Canonical.str "investigationId" e |> Option.get
                    Result.isError (StorageReader.verify Kernel.domain (Some(InvestigationId(Guid id))) (WorkspaceAddress.catalog (Workspace "fixtures/v1.0.0")) (CommitOid "t") (FixtureData.frozenFiles id)))
                |> List.length)
        let reconstruct (p: GitHubProvider) () =
            FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
            match ProviderPort.catalog p with
            | Ok(_, rs) -> rs |> List.filter (ProviderPort.digestMatches >> not) |> List.length
            | Error _ -> 1
        let contract = timed (reconstruct (fst (FixtureData.fixtureProvider [])))
        let live = timed (reconstruct real)
        let facts =
            [ for name, (ms, failures) in [ "domain", domain; "provider-contract", contract; "live-integration", live ] do
                  yield name + ".ms", string ms
                  yield name + ".failures", string failures ]
        let line = ProviderEvidence.record real "PF-012-O3" [] facts
        for name in [ "domain"; "provider-contract"; "live-integration" ] do
            Assert.Contains(sprintf "\"%s.ms\"" name, line)
            Assert.Contains(sprintf "\"%s.failures\":\"0\"" name, line)

    [<Fact>]
    let ``PF-012-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-012-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-012-O3`` () = o3 PfKit.live
