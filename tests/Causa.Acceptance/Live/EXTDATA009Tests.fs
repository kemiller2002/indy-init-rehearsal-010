// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// EXT-DATA-009 (live): a clean install holds no investigation; an authorized, separately populated test repository is
// read; unauthorized access is denied; malformed data is refused; create, read, write and reload work; the deployed
// exact SHA is verified.
namespace Causa.Acceptance.Live

open System
open System.Text
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module EXTDATA009Tests =
    /// Investigation ids and titles of the causa-data datasets (catalog and frozen fixtures).
    let private datasetMarkers () =
        let catalogIds = Datasets.catalog.Value |> Map.toList |> List.choose (fun (p, _) -> if p.EndsWith "/manifest.json" && p.Contains "/investigations/" then Some(p.Split('/')[4]) else None)
        let fixtureIds = FixtureData.frozenEntries () |> List.choose (Canonical.str "investigationId")
        let titles = FixtureData.frozenEntries () |> List.choose (Canonical.str "title")
        catalogIds @ fixtureIds @ titles @ [ "Apollo 11 lunar landing"; "Burned-out ceiling lightbulb"; "Challenger (STS-51-L)" ]

    let private productionText () =
        PfKit.sources "src" [ ".fs"; ".cs"; ".ts"; ".js"; ".html"; ".json" ] @ PfKit.sources "web" [ ".html"; ".js"; ".css"; ".json" ]

    /// O1: clean install with zero production investigation fixtures and no preloaded records; authorized retrieval
    /// from a separately populated repository; denial without authorization; malformed data refused; create, read,
    /// write and reload.
    let o1 (mk: string -> GitHubProvider) =
        // Zero production fixtures: no source, web asset or production assembly holds a dataset id, title or history.
        let markers = datasetMarkers ()
        for path, text in productionText () do
            for m in markers do
                Assert.False(text.Contains m, sprintf "%s holds investigation data (%s)" path m)
        for a in [ typeof<DomainKernel>.Assembly; typeof<SaveMachine>.Assembly; typeof<Causa.App.Model>.Assembly ] do
            Assert.Empty(a.GetManifestResourceNames() |> Array.filter (fun n -> n.EndsWith ".ndjson" || n.EndsWith ".json"))
        // No preloaded records: a fresh application holds no investigation, listing or connection.
        let p = mk "extdata009-o1"
        let app = AppDriver.start p
        Assert.True(app.Model.Loads.IsEmpty && app.Model.Sessions.IsEmpty && app.Model.Listings.IsEmpty && app.Model.Context.IsNone)
        Assert.Empty(app.Sent)
        // Authorized retrieval from the separately populated repository (populated by its own commit, not the app).
        FixtureData.seed p (FixtureData.frozenCatalog ()) "Causa: populate the test repository" |> ignore
        AppDriver.connect app p.Connection.Credential
        AppDriver.listCatalog app
        let listing = app.Model.Listings |> Map.toList |> List.map snd |> List.find (fun l -> l.Address.Role = WorkspaceRole.Catalog)
        Assert.Equal(5, listing.Investigations.Length)
        AppDriver.openInvestigation app listing.Investigations.Head.Ref
        match app.Model.Loads.TryFind listing.Investigations.Head.Ref with
        | Some(Loaded v) -> Assert.Equal(listing.Investigations.Head.ExpectedStateSha256, Some(ProviderPort.stateDigest v))
        | other -> failwithf "the authorized read did not load: %A" other
        // Denial without authorization: a refused credential reads nothing.
        let denied = AppDriver.start p
        AppDriver.connect denied (Credential.create "invalid-credential-for-conformance")
        AppDriver.listCatalog denied
        Assert.True(denied.Model.Listings.IsEmpty && denied.Model.Loads.IsEmpty)
        Assert.NotEqual(app.Model.Connection, denied.Model.Connection)
        // Malformed data is refused fail-closed.
        let malformed = FixtureData.edgeCases () |> List.find (fun c -> c.Name.StartsWith "invalid schema")
        Assert.True(FixtureData.meets malformed (FixtureData.runEdgeCase p "extdata009" malformed))
        // Create, read, write, reload: a new investigation saved by the application, edited and saved again, then
        // opened in a new tab from GitHub alone.
        let run = LiveRun.createOn p "extdata009"
        LiveRun.edit run "written after create" |> ignore
        LiveRun.input run SaveInput.SaveRequested |> ignore
        Assert.Equal("Saved", MachineKit.stateName run.Session.State)
        match LiveRun.openInNewTab run with
        | Ok v ->
            Assert.Equal(AggregateVersion 2L, v.Investigation.Version)
            Assert.Equal(run.Session.BaseHead, v.ReadAt)
        | Error f -> failwithf "reload: %s" (ProviderPort.failureCode f)
        ProviderEvidence.record p "EXT-DATA-009-O1" [ run.Session.BaseHead ] [] |> ignore

    /// O2: the deployed exact SHA is verified: the deployed application serves the bytes this candidate built, and the
    /// candidate is the commit under test. Without a deployment to check this is BLOCKED.
    let o2 (mk: string -> GitHubProvider) =
        let _ = mk "extdata009-o2"
        let env name = match Environment.GetEnvironmentVariable name with null | "" -> None | v -> Some v
        match env "CAUSA_APP_URL", env "CAUSA_CANDIDATE" with
        | Some url, Some candidate ->
            Assert.Matches("^[0-9a-f]{40}$", candidate)
            Assert.Equal(candidate, PfKit.sourceSha ())
            let built = IO.Path.Combine(Layers.repositoryRoot (), "dist", "index.html")
            Assert.True(IO.File.Exists built, "the candidate's build (dist/index.html) is not present to compare with the deployment")
            use client = new Net.Http.HttpClient()
            let served = client.GetByteArrayAsync(Uri(Uri url, "index.html")).GetAwaiter().GetResult()
            Assert.Equal<byte[]>(IO.File.ReadAllBytes built, served)
        | _ -> failwith "BLOCKED: CAUSA_APP_URL and CAUSA_CANDIDATE name no deployment of the candidate to verify (EXT-DATA-009-O2). This is a failure, not a pass."

    [<Fact>]
    let ``EXT-DATA-009-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``EXT-DATA-009-O2`` () = o2 PfKit.live
