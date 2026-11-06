// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-005 (live): provider isolation. Domain and UI checks never need GitHub; live checks never fall back to fixtures.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF005Tests =
    let private networkMarkers = [ "api.github.com"; LiveGitHub.TokenVariable; "LiveGitHub"; "HttpClient"; "GH_TOKEN"; "GITHUB_TOKEN" ]

    /// O1: UI and domain tests depend on no GitHub, network or credential: their sources name none, their suites run
    /// in the offline environments, the domain assembly references no network stack, and the domain verification of
    /// the frozen fixtures succeeds while the provider is unreachable.
    let o1 (mk: string -> GitHubProvider) =
        let sources =
            PfKit.sources "tests/Causa.Domain.Tests" [ ".fs" ] @ PfKit.sources "tests/Causa.App.Tests" [ ".fs" ] @ (PfKit.sources "tests/browser" [ ".js"; ".mjs"; ".ts" ] |> List.filter (fun (p, _) -> not (p.Replace('\\', '/').Contains "/live/")))
        Assert.NotEmpty(sources)
        for path, text in sources do
            for marker in networkMarkers do
                Assert.False(text.Contains marker, sprintf "%s names %s" path marker)
        use plan = Layers.plan ()
        for s in Layers.suites plan do
            match Layers.ofSuite s.Id with
            | Some Layers.Domain -> Assert.Equal("ci", s.Environment)
            | Some Layers.Ui when not (s.Id.Contains "LIVE") -> Assert.Equal("ci-browser", s.Environment)
            | _ -> ()
        let refs = typeof<DomainKernel>.Assembly.GetReferencedAssemblies() |> Array.map (fun a -> string a.Name)
        Assert.DoesNotContain("System.Net.Http", refs)
        Assert.DoesNotContain("Causa.Persistence", refs)
        let real = mk "pf005-o1"
        let unreachable = { real with Execute = fun r -> NotAnswered(r.Id, FailedBeforeSending) }
        Assert.Equal(Error(NetworkUnavailable FailedBeforeSending), Scenarios.readHead unreachable)
        for e in FixtureData.frozenEntries () do
            let id = Canonical.str "investigationId" e |> Option.get
            let address = WorkspaceAddress.catalog (Workspace "fixtures/v1.0.0")
            Assert.True(Result.isOk (StorageReader.verify Kernel.domain (Some(InvestigationId(Guid id))) address (CommitOid "offline") (FixtureData.frozenFiles id)))

    /// O2: a real-provider test never falls back to a fixture or reports a mocked result as live: without the
    /// credential it is BLOCKED (a failure); the live provider is always LiveProvider and answers with GitHub's own
    /// request id; only a live provider's evidence can say "live".
    let o2 (mk: string -> GitHubProvider) =
        match LiveGitHub.configurationFrom (fun _ -> None) with
        | Error reason ->
            Assert.StartsWith("BLOCKED", reason)
            Assert.Contains("not a pass", reason)
        | Ok _ -> failwith "no credential must be BLOCKED"
        let real = mk "pf005-o2"
        let fixture, _ = FixtureData.fixtureProvider []
        let answer = real.Execute(GitHubRequests.branchHead (Scenarios.nextId ()) real.Connection)
        Assert.Equal(real.Mode = LiveProvider, PfKit.fromGitHub answer)
        Assert.DoesNotContain("\"verified\":\"live\"", ProviderEvidence.line fixture "PF-005-O2" [] [])
        Assert.Equal(real.Mode = LiveProvider, (ProviderEvidence.line real "PF-005-O2" [] []).Contains "\"verified\":\"live\"")

    [<Fact>]
    let ``PF-005-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-005-O2`` () = o2 PfKit.live
