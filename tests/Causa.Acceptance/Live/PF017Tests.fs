// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-017 (live): failure isolation. Every failure is reported in one layer; a provider outage does not fail offline
// checks, and an offline pass does not waive a failing integration gate.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF017Tests =
    /// O1: every planned test belongs to a suite of exactly one layer (domain, UI, provider contract, live integration
    /// or process), so its failure is reported in that layer; a provider failure is reported as the provider's, never
    /// as a domain validation failure.
    let o1 (mk: string -> GitHubProvider) =
        use plan = Layers.plan ()
        let suites = Layers.suites plan
        Assert.All(suites, fun s -> Assert.True((Layers.ofSuite s.Id).IsSome, sprintf "%s has no layer" s.Id))
        let tests = Layers.tests plan
        Assert.All(tests, fun t -> Assert.Contains(suites, fun s -> s.Id = t.Suite))
        let layers = tests |> List.choose (fun t -> Layers.ofSuite t.Suite) |> List.distinct
        for layer in [ Layers.Domain; Layers.Ui; Layers.ProviderContract; Layers.LiveIntegration ] do
            Assert.Contains(layer, layers)
        let real = mk "pf017-o1"
        let refused = { real with Connection = { real.Connection with Credential = Credential.create "invalid-credential-for-conformance" } }
        match ProviderPort.list refused ActiveCatalog with
        | Error(ProviderFailed AuthenticationFailed) -> ()
        | other -> failwithf "a refused credential is a provider failure, not %A" (other |> Result.map ignore)

    /// O2: a provider outage leaves the offline domain checks green (they verify without the provider) while it fails
    /// the live layer; and no offline suite stands in for the live gate: the live suites are separate, depend on the
    /// offline ones, and release runs every suite.
    let o2 (mk: string -> GitHubProvider) =
        let real = mk "pf017-o2"
        let outage = { real with Execute = fun r -> NotAnswered(r.Id, FailedBeforeSending) }
        match ProviderPort.list outage ActiveCatalog with
        | Error(ProviderFailed(NetworkUnavailable FailedBeforeSending)) -> ()
        | other -> failwithf "an outage fails the live layer: %A" (other |> Result.map ignore)
        for e in FixtureData.frozenEntries () do
            let id = Canonical.str "investigationId" e |> Option.get
            Assert.True(Result.isOk (StorageReader.verify Kernel.domain (Some(InvestigationId(Guid id))) (WorkspaceAddress.catalog (Workspace "fixtures/v1.0.0")) (CommitOid "offline") (FixtureData.frozenFiles id)))
        use plan = Layers.plan ()
        let suites = Layers.suites plan
        for offline in [ "SU-OWN-DOTNET"; "SU-DOTNET-DETERMINISTIC"; "SU-DOTNET-INVARIANTS" ] do
            Assert.All(Layers.ancestors suites offline, fun a -> Assert.NotEqual(Some Layers.LiveIntegration, Layers.ofSuite a))
        Assert.Contains("SU-DOTNET-DETERMINISTIC", Layers.ancestors suites "SU-DOTNET-LIVE")
        let gate = IO.File.ReadAllText(IO.Path.Combine(Layers.repositoryRoot (), ".github", "workflows", "verification-gate.yml"))
        Assert.Contains("suites=\"--require-tests true\"", gate)

    [<Fact>]
    let ``PF-017-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-017-O2`` () = o2 PfKit.live
