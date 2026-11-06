// Owner: persistence. EXT-DATA-013 (live): the active demonstration catalog named by causa-data catalog/active.json, its
// four investigations loaded after an explicit Connect through the full validation and replay pipeline to the states
// its manifest records; the five synthetic v1.0.0 fixtures stay the storage contract's test corpus only.
namespace Causa.Acceptance.Live

open System
open System.Net.Http
open System.Text
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module EXTDATA013Tests =
    let private causaData = "kemiller2002/causa-data"

    /// A file of causa-data as GitHub serves it at `revision` (the public repository, read over the network).
    let private fetch (revision: string) (path: string) : byte[] =
        use client = new HttpClient(Timeout = TimeSpan.FromSeconds 30.0)
        client.GetByteArrayAsync(sprintf "https://raw.githubusercontent.com/%s/%s/%s" causaData revision path).GetAwaiter().GetResult()

    let private json (bytes: byte[]) = Canonical.parse (Encoding.UTF8.GetString(bytes).TrimEnd('\n')) |> Result.defaultWith failwith

    let private cases (manifest: Canonical.JsonValue) =
        match Canonical.field "cases" manifest with
        | Some(Canonical.JArray xs) -> xs
        | _ -> []

    let private required = [ "Apollo 1 (AS-204)"; "Apollo 11"; "Challenger (STS-51-L)"; "lightbulb" ]

    /// Connect, list the active catalog and open each investigation through the application engine.
    let private connectAndLoad (p: GitHubProvider) =
        let app = AppDriver.start p
        AppDriver.connect app p.Connection.Credential
        let listing =
            app.Model.Listings |> Map.toList |> List.map snd |> List.tryFind (fun l -> l.Address.Role = WorkspaceRole.Catalog)
            |> Option.defaultWith (fun () -> failwithf "Connect listed no active catalog: %A" app.Announced)
        for i in listing.Investigations do
            AppDriver.openInvestigation app i.Ref
        app, listing

    /// O1: the active catalog is the one causa-data's catalog/active.json names, at the pinned commit and on main:
    /// exactly four investigations, Apollo 1 (AS-204), Apollo 11, Challenger (STS-51-L) and the burned-out lightbulb;
    /// the catalog the tests write is byte-identical to it.
    let o1 (mk: string -> GitHubProvider) =
        let p = mk "extdata013-o1"
        for revision in [ FixtureData.sourceCommit; "main" ] do
            let active = json (fetch revision "catalog/active.json")
            let manifestPath = Canonical.str "manifest" active |> Option.get
            Assert.Equal(Some "catalog/demonstration-v1/manifest.json", Some manifestPath)
            let manifest = json (fetch revision manifestPath)
            let titles = cases manifest |> List.choose (Canonical.str "title")
            Assert.Equal(4, titles.Length)
            for name in required do
                Assert.True(titles |> List.exists (fun t -> t.Contains name), sprintf "%s is not a case of the active catalog at %s" name revision)
        for path in [ "catalog/active.json"; "catalog/demonstration-v1/manifest.json" ] do
            Assert.Equal<byte[]>(fetch FixtureData.sourceCommit path, Datasets.catalog.Value[path])
        FixtureData.seed p (FixtureData.demonstrationCatalog ()) "Causa: causa-data active catalog" |> ignore
        let _, listing = connectAndLoad p
        Assert.Equal(4, listing.Investigations.Length)
        Assert.Equal(Workspace(Canonical.str "workspace" (json Datasets.catalog.Value["catalog/active.json"]) |> Option.get), listing.Address.Workspace)

    /// O2: after an explicit Connect, all four load through validation and replay to the states the manifest records; a
    /// synthetic investigation placed in the catalog workspace is never presented or loaded as a catalog case; no
    /// production file bundles any catalog record.
    let o2 (mk: string -> GitHubProvider) =
        let p = mk "extdata013-o2"
        let synthetic = "6fd2ddc9-bedd-5fff-9093-5506c4d68cb3"
        let fs = FixtureData.frozenFiles synthetic
        let dir = sprintf "workspaces/demo/demonstration-v1/investigations/%s" synthetic
        FixtureData.seed p (FixtureData.demonstrationCatalog () @ [ dir + "/manifest.json", fs.Manifest; dir + "/events.ndjson", fs.Events; dir + "/snapshot.json", fs.Snapshot ]) "Causa: catalog plus a stray synthetic investigation" |> ignore
        // Nothing is listed or loaded before the explicit Connect.
        let before = AppDriver.start p
        Assert.True(before.Model.Listings.IsEmpty && before.Model.Loads.IsEmpty && before.Sent.IsEmpty)
        let app, listing = connectAndLoad p
        let manifest = json Datasets.catalog.Value["catalog/demonstration-v1/manifest.json"]
        Assert.Equal(4, listing.Investigations.Length)
        for i in listing.Investigations do
            match app.Model.Loads.TryFind i.Ref with
            | Some(Loaded v) ->
                let (InvestigationId g) = v.Investigation.Id
                let case = cases manifest |> List.find (fun c -> Canonical.str "investigationId" c = Some(g.ToString "D"))
                Assert.Equal(Canonical.str "expectedStateSha256" case, Some(ProviderPort.stateDigest v))
                Assert.Equal(Ok v.Investigation, Kernel.domain.Replay v.History)
                Assert.Equal(None, Persistence.sessionOf v)
            | other -> failwithf "%A did not load: %A" i.Title other
        Assert.DoesNotContain(listing.Investigations, fun i -> i.Ref.Id = InvestigationId(Guid synthetic))
        let stray: InvestigationRef = { Address = listing.Address; Id = InvestigationId(Guid synthetic) }
        match ProviderPort.openRef p stray with
        | Error(Unverifiable [ ContractValueMismatch("investigationId", _, _) ]) -> ()
        | other -> failwithf "a synthetic investigation was presented as a catalog case: %A" (other |> Result.map ignore)
        let ids = cases manifest |> List.choose (Canonical.str "investigationId")
        for path, text in PfKit.sources "src" [ ".fs"; ".cs"; ".js"; ".ts"; ".html"; ".json" ] @ PfKit.sources "web" [ ".js"; ".html"; ".json" ] do
            for id in ids do
                Assert.False(text.Contains id, sprintf "%s bundles catalog record %s" path id)

    /// O3: the five synthetic v1.0.0 fixtures are the storage contract's test corpus only: none is a catalog case, none
    /// is in a production assembly or source, and they verify through the storage reader as the contract corpus.
    let o3 (mk: string -> GitHubProvider) =
        let p = mk "extdata013-o3"
        let synthetic = FixtureData.frozenEntries () |> List.choose (Canonical.str "investigationId")
        Assert.Equal(5, synthetic.Length)
        let live = cases (json (fetch "main" "catalog/demonstration-v1/manifest.json")) |> List.choose (Canonical.str "investigationId")
        Assert.Empty(Set.intersect (set synthetic) (set live))
        for a in [ typeof<DomainKernel>.Assembly; typeof<SaveMachine>.Assembly; typeof<Causa.App.Model>.Assembly ] do
            Assert.DoesNotContain(a.GetTypes(), fun t -> t.Name = "Datasets")
        for path, text in PfKit.sources "src" [ ".fs"; ".cs"; ".js"; ".ts"; ".html"; ".json" ] do
            for id in synthetic do
                Assert.False(text.Contains id, sprintf "%s holds synthetic fixture %s" path id)
        for id in synthetic do
            Assert.True(Result.isOk (StorageReader.verify Kernel.domain (Some(InvestigationId(Guid id))) (WorkspaceAddress.catalog (Workspace "fixtures/v1.0.0")) (CommitOid "corpus") (FixtureData.frozenFiles id)))
        // On GitHub, the active catalog never lists them.
        FixtureData.seed p (FixtureData.demonstrationCatalog ()) "Causa: causa-data active catalog" |> ignore
        match ProviderPort.list p ActiveCatalog with
        | Ok listing -> Assert.All(listing.Investigations, fun i -> Assert.DoesNotContain((let (InvestigationId g) = i.Ref.Id in g.ToString "D"), synthetic))
        | Error f -> failwith (ProviderPort.failureCode f)

    [<Fact>]
    let ``EXT-DATA-013-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``EXT-DATA-013-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``EXT-DATA-013-O3`` () = o3 PfKit.live
