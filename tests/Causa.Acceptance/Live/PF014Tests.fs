// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// PF-014 (live): live integration acceptance at the integration checkpoints: the approved repository, the pinned
// catalog, four reconstructions through the application engine, and an honest record of what ran where.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PF014Tests =
    let private env name = match Environment.GetEnvironmentVariable name with null | "" -> None | v -> Some v
    let private sourceSha = PfKit.sourceSha
    let private runId = PfKit.runId

    /// The pinned demonstration catalog written to the branch, then discovered, listed and opened by the application
    /// engine exactly as the user does: Connect, list the active catalog, open each investigation.
    let private checkpoint (p: GitHubProvider) =
        let commit = FixtureData.seed p (FixtureData.demonstrationCatalog ()) "Causa: pinned demonstration-v1 catalog"
        let app = EngineDriver.start p
        EngineDriver.connect app p.Connection.Credential
        EngineDriver.listCatalog app
        let listing =
            app.Model.Listings
            |> Map.toList
            |> List.map snd
            |> List.tryFind (fun l -> l.Address.Role = WorkspaceRole.Catalog)
            |> Option.defaultWith (fun () -> failwithf "the engine listed no catalog: %A" app.Announced)
        for i in listing.Investigations do
            EngineDriver.openInvestigation app i.Ref
        commit, app, listing

    /// O1: the real GitHub provider test runs against the approved repository (the configured test repository) and the
    /// pinned catalog (causa-data at the pinned commit).
    let o1 (mk: string -> GitHubProvider) =
        let p = mk "pf014-o1"
        let approved = LiveGitHub.configurationFrom (fun n -> if n = LiveGitHub.TokenVariable then Some "x" else env n) |> Result.map snd
        match approved with
        | Ok repo when p.Mode = LiveProvider -> Assert.Equal((repo.Owner, repo.Name), (p.Connection.Repository.Owner, p.Connection.Repository.Name))
        | _ -> ()
        Assert.Equal("e8259df74add723765c57a5b1a5888d9c3f707af", FixtureData.sourceCommit)
        let commit, _, listing = checkpoint p
        Assert.Equal(commit, listing.ReadAt)
        let message = Scenarios.commitMessage p commit |> Probes.ok "message"
        Assert.Contains(FixtureData.sourceCommit, message)

    /// O2: actual network communication, each answer associated with its own request, the four catalog investigations
    /// reconstructed by the application to their expected versions and digests.
    let o2 (mk: string -> GitHubProvider) =
        let p = mk "pf014-o2"
        let _, app, listing = checkpoint p
        Assert.Equal(4, listing.Investigations.Length)
        let ids = app.Sent |> List.map (fun r -> r.Id)
        Assert.Equal(ids.Length, (List.distinct ids).Length)
        for request, outcome in List.zip app.Sent app.Outcomes do
            match outcome with
            | Answered(id, _, _, _) -> Assert.Equal(request.Id, id)
            | NotAnswered(id, _) -> failwithf "request %A was not answered" id
            Assert.Equal(p.Mode = LiveProvider, PfKit.fromGitHub outcome)
        let manifest = Canonical.parse (Text.Encoding.UTF8.GetString(Datasets.catalog.Value["catalog/demonstration-v1/manifest.json"]).TrimEnd('\n')) |> Result.defaultWith failwith
        let cases = match Canonical.field "cases" manifest with Some(Canonical.JArray xs) -> xs | _ -> []
        for i in listing.Investigations do
            match app.Model.Loads.TryFind i.Ref with
            | Some(Loaded v) ->
                let (InvestigationId g) = v.Investigation.Id
                let case = cases |> List.find (fun c -> Canonical.str "investigationId" c = Some(g.ToString "D"))
                let (AggregateVersion n) = v.Investigation.Version
                Assert.Equal(Canonical.integer "expectedVersion" case, Some n)
                Assert.Equal(Canonical.str "expectedStateSha256" case, Some(ProviderPort.stateDigest v))
                Assert.Equal(i.ExpectedStateSha256, Some(ProviderPort.stateDigest v))
            | other -> failwithf "%A did not load: %A" i.Title other

    /// O3: the run records the repository ref, the source SHA, the run id and the provider mode.
    let o3 (mk: string -> GitHubProvider) =
        let p = mk "pf014-o3"
        let commit, _, listing = checkpoint p
        let repo = p.Connection.Repository
        let source = sourceSha ()
        let run = runId ()
        let line =
            ProviderEvidence.record p "PF-014-O3" [ commit ]
                [ "repositoryRef", sprintf "%s/%s@refs/heads/%s" repo.Owner repo.Name repo.Branch
                  "head", (let (CommitOid c) = listing.ReadAt in c)
                  "sourceSha", source
                  "catalogSha", FixtureData.sourceCommit
                  "runId", run
                  "providerMode", ProviderEvidence.modeName p.Mode ]
        for expected in [ "\"repositoryRef\""; "\"sourceSha\":\"" + source; "\"runId\":\"" + run; "\"providerMode\":\"" + ProviderEvidence.modeName p.Mode ] do
            Assert.Contains(expected, line)
        Assert.Matches("^[0-9a-f]{40}$|^unknown$", source)

    /// O4: live success is never claimed from mocks: a fixture run's evidence says so, and a live run passes only on
    /// GitHub's own answers.
    let o4 (mk: string -> GitHubProvider) =
        let p = mk "pf014-o4"
        let fixture, _ = FixtureData.fixtureProvider []
        let _, fixtureApp, _ = checkpoint fixture
        Assert.All(fixtureApp.Outcomes, fun o -> Assert.False(PfKit.fromGitHub o))
        Assert.Contains("fake-only, not live verification", ProviderEvidence.line fixture "PF-014-O4" [] [])
        let _, app, _ = checkpoint p
        if p.Mode = LiveProvider then
            Assert.All(app.Outcomes, fun o -> Assert.True(PfKit.fromGitHub o, "a live pass needs GitHub's own answers"))
            Assert.Contains("\"verified\":\"live\"", ProviderEvidence.line p "PF-014-O4" [] [])

    [<Fact>]
    let ``PF-014-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-014-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-014-O3`` () = o3 PfKit.live

    [<Fact>]
    let ``PF-014-O4`` () = o4 PfKit.live
