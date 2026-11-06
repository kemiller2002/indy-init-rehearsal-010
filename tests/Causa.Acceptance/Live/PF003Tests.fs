// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-003 (live): explicit provider selection: a local fixture mode and the real GitHub mode, never confused.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PF003Tests =
    /// O1: both modes are explicit, each with its own documented, repeatable command in the governed plan: the fixture
    /// suite runs the stateful fake, the live suite the authorized GitHub test repository, and the providers say which.
    let o1 (mk: string -> GitHubProvider) =
        use plan = Layers.plan ()
        let suites = Layers.suites plan
        let suite id = suites |> List.find (fun s -> s.Id = id)
        let fixtureSuite = suite "SU-DOTNET-DETERMINISTIC"
        let liveSuite = suite "SU-DOTNET-LIVE"
        Assert.StartsWith("dotnet test", fixtureSuite.Command)
        Assert.Contains("Causa.Acceptance.Deterministic.", fixtureSuite.Command)
        Assert.Equal("ci", fixtureSuite.Environment)
        Assert.StartsWith("dotnet test", liveSuite.Command)
        Assert.Contains("Causa.Acceptance.Live.", liveSuite.Command)
        Assert.Equal("live-github", liveSuite.Environment)
        let fixture, _ = FixtureData.fixtureProvider []
        Assert.Equal(FakeProvider, fixture.Mode)
        let real = mk "pf003-o1"
        let answer = real.Execute(GitHubRequests.branchHead (Scenarios.nextId ()) real.Connection)
        // The real mode is GitHub's own answer; the fixture's never is.
        Assert.Equal(real.Mode = LiveProvider, PfKit.fromGitHub answer)
        Assert.False(PfKit.fromGitHub (fixture.Execute(GitHubRequests.branchHead (Scenarios.nextId ()) fixture.Connection)))

    /// O2: fixture mode needs no network, no GitHub credential, no database and no external service: with no live
    /// credential configured it discovers and reconstructs the whole fixture catalog in process.
    let o2 (mk: string -> GitHubProvider) =
        let _ = mk "pf003-o2"
        Assert.True(Result.isError (LiveGitHub.configurationFrom (fun _ -> None)))
        let fixture, running = FixtureData.fixtureProvider (FixtureData.frozenCatalog ())
        match ProviderPort.catalog fixture with
        | Error f -> failwith (ProviderPort.failureCode f)
        | Ok(_, rs) ->
            Assert.Equal(5, rs.Length)
            Assert.All(rs, fun r -> Assert.True(ProviderPort.digestMatches r))
        // Every request was answered by the in-process fake (logged there), none by GitHub.
        Assert.NotEmpty(running.Current().Log)
        Assert.All(running.Current().Log, fun e -> Assert.True(e.Operation.IsSome))
        Assert.DoesNotContain(LiveGitHub.TokenVariable, sprintf "%A" fixture.Connection)

    /// O3: the fixture mode cannot activate in a production deployment: the deployed assemblies contain no fixture
    /// provider and do not reference the test support that holds it; the live provider never answers from a fixture.
    let o3 (mk: string -> GitHubProvider) =
        let production = [ typeof<Causa.Domain.DomainKernel>.Assembly; typeof<SaveMachine>.Assembly; typeof<Causa.App.Model>.Assembly ]
        for a in production do
            Assert.DoesNotContain("Causa.TestSupport", a.GetReferencedAssemblies() |> Array.map (fun n -> string n.Name))
            Assert.DoesNotContain(a.GetTypes(), fun t -> let n = string t.FullName in n.Contains "StatefulFake" || n.Contains "FixtureData" || t.Name = "GitHubProvider")
        let real = mk "pf003-o3"
        let outcome = real.Execute(GitHubRequests.branchHead (Scenarios.nextId ()) real.Connection)
        if real.Mode = LiveProvider then Assert.True(PfKit.fromGitHub outcome, "the live provider's answer must come from GitHub")
        Assert.Equal(LiveProvider, (LiveGitHub.provider () |> Result.map (fun p -> p.Mode) |> Result.defaultValue LiveProvider))

    /// O4: the active mode is stated wherever results are reported, and fixture data is never labelled live: fixture
    /// evidence says "fake-only, not live verification" and fixture views carry a "FIXTURE:" label.
    let o4 (mk: string -> GitHubProvider) =
        let fixture, _ = FixtureData.fixtureProvider []
        let real = mk "pf003-o4"
        let fixtureLine = ProviderEvidence.line fixture "PF-003-O4" [] []
        Assert.Contains("\"mode\":\"fake\"", fixtureLine)
        Assert.Contains("fake-only, not live verification", fixtureLine)
        Assert.DoesNotContain("\"verified\":\"live\"", fixtureLine)
        let realLine = ProviderEvidence.line real "PF-003-O4" [] []
        Assert.Equal(real.Mode = LiveProvider, realLine.Contains "\"verified\":\"live\"")
        let label = Causa.Ui.Contracts.FixtureLabel.create "populated investigation"
        Assert.StartsWith("FIXTURE: ", Causa.Ui.Contracts.FixtureLabel.value label)
        Assert.Equal(Some label, Causa.Ui.Contracts.Visible.fixtureLabel (Causa.Ui.Contracts.Visible.fixture label 1))
        ProviderEvidence.record real "PF-003-O4" [] [ "mode", ProviderEvidence.modeName real.Mode ] |> ignore

    [<Fact>]
    let ``PF-003-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-003-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-003-O3`` () = o3 PfKit.live

    [<Fact>]
    let ``PF-003-O4`` () = o4 PfKit.live
