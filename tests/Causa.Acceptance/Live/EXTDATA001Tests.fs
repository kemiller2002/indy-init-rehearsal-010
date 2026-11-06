// Owner: integration. EXT-DATA-001 (no bundled investigation content) and the application driver every EXT-DATA live
// test uses: one browser tab's engine (Update.stepAt) whose Http effects are executed against a GitHub provider (the
// real test repository in the live suite) and whose answers are fed back, exactly as the Limen host does.
namespace Causa.Acceptance.Live

open System
open System.IO
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport
open Causa.App

/// One tab of the application driven against a provider.
[<RequireQualifiedAccess>]
module AppDriver =
    type Tab =
        { Provider: GitHubProvider
          mutable Model: Model
          /// Every request the tab sent, in order.
          mutable Sent: HttpRequest list
          /// Every answer the tab received.
          mutable Answers: HttpOutcome list
          /// Every host effect the engine asked for (Http included).
          mutable Effects: HostEffect list }

    let tab (provider: GitHubProvider) : Tab =
        { Provider = provider
          Model = Update.initialModel (Route "#/investigations")
          Sent = []
          Answers = []
          Effects = [] }

    let private https (effects: HostEffect list) = effects |> List.choose (function Http r -> Some r | _ -> None)

    /// Executes every request the engine asked for, feeding each answer back, until nothing is in flight.
    let rec private pump (t: Tab) (pending: HttpRequest list) =
        match pending with
        | [] -> ()
        | request :: rest ->
            t.Sent <- t.Sent @ [ request ]
            let outcome = t.Provider.Execute request
            t.Answers <- t.Answers @ [ outcome ]
            let model, effects = Update.stepAt DateTimeOffset.UtcNow (Browser(HttpCompleted outcome)) t.Model
            t.Model <- model
            t.Effects <- t.Effects @ effects
            pump t (rest @ https effects)

    /// One engine message, then every request it leads to. Returns the effects of that message alone.
    let step (t: Tab) (msg: Msg) : HostEffect list =
        let model, effects = Update.stepAt DateTimeOffset.UtcNow msg t.Model
        t.Model <- model
        t.Effects <- t.Effects @ effects
        let before = t.Effects.Length
        pump t (https effects)
        effects @ (t.Effects |> List.skip before)

    /// Connect with the provider's repository and credential, as the Connect form submits them.
    let connect (t: Tab) =
        step t (Connection(ConnectionInput.ConnectSubmitted(t.Provider.Connection.Repository, t.Provider.Connection.Credential)))

    let workingCopy (t: Tab) : Writable.T =
        t.Model.Listings
        |> Map.toList
        |> List.tryPick (fun (a, _) -> Writable.ofAddress a)
        |> Option.defaultWith (fun () -> failwithf "no working copy was listed (connection %A)" t.Model.Connection)

    let text (s: string) = (NonEmptyText.tryCreate s).Value

    /// Starts a new investigation in the listed working copy, as the Start screen does (C1); returns its reference.
    let start (t: Tab) (title: string) (problem: string) : InvestigationRef =
        let w = workingCopy t
        let id = InvestigationId(Guid.NewGuid())
        let command =
            { CommandId = Guid.NewGuid()
              Investigation = id
              ExpectedVersion = None
              Command = Core(StartInvestigation { Title = text title; ObservationId = ObservationId(Guid.NewGuid()); Original = text problem }) }
        step t (CreateRequested(w, command)) |> ignore
        { Address = Writable.address w; Id = id }

    let save (t: Tab) (r: InvestigationRef) = step t (SessionInput(r, SaveInput.SaveRequested))

    let saveState (t: Tab) (r: InvestigationRef) =
        t.Model.Sessions |> Map.tryFind r |> Option.map (fun s -> MachineKit.stateName s.State) |> Option.defaultValue "<none>"

    let mutations (t: Tab) = t.Sent |> List.filter SaveKit.isMutation

    let announcements (t: Tab) = t.Effects |> List.choose (function LiveAnnounce(_, s) -> Some s | _ -> None)

    let head (p: GitHubProvider) = Scenarios.readHead p |> Probes.ok "head"

    /// The live provider on a fresh branch of the test repository, or the BLOCKED failure (never the fake).
    let live (label: string) : GitHubProvider = LiveGitHub.freshBranch (LiveGitHub.require ()) label

    /// The fake provider over a fresh repository (for proving a test body; never used by a live Fact).
    let fake (_: string) : GitHubProvider = fst (Scenarios.fakeProvider "main" [ "README.md", GitHubRequests.utf8 "test repository\n" ])

    /// Commits files directly to the provider's branch (another writer, or forged content).
    let commitFiles (p: GitHubProvider) (files: (string * string) list) =
        let request = Scenarios.commitRequest (head p) files ("forged-" + Guid.NewGuid().ToString("N"))
        match Scenarios.commit p request with
        | _, Ok oid -> oid
        | _, Error e -> failwithf "commit: %s" (Classify.describe e)

/// The checked-out repository the tests run in (for the checks of what ships).
[<RequireQualifiedAccess>]
module Repo =
    let root =
        lazy
            (let rec up (d: string) =
                if File.Exists(Path.Combine(d, "Causa.slnx")) then d
                else
                    match Path.GetDirectoryName d with
                    | null -> failwith "Causa.slnx not found above the test directory"
                    | parent when parent = d -> failwith "Causa.slnx not found above the test directory"
                    | parent -> up parent
             up AppContext.BaseDirectory)

    let at (relative: string) = Path.Combine(root.Value, relative)

    /// Every file under the shipped trees (application source, web assets and, when built, dist/).
    let shipped () =
        [ "src"; "web"; "dist" ]
        |> List.map at
        |> List.filter Directory.Exists
        |> List.collect (fun d -> Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories) |> List.ofSeq)
        |> List.filter (fun f ->
            let s = f.Replace('\\', '/')
            not (s.Contains "/bin/" || s.Contains "/obj/"))

    /// The application's own shipped files (its source and web assets, and the built app's copies of them), without
    /// the vendored Limen, Forma and .NET runtime files.
    let own () =
        shipped ()
        |> List.filter (fun f ->
            let s = f.Replace('\\', '/')
            not (s.Contains "/dist/limen/" || s.Contains "/dist/forma/" || s.Contains "/dist/_framework/" || s.Contains "/dist/folio/"))

    /// The text files among them (binary .wasm/.dll/.dat are searched as UTF-8 too: a string literal survives).
    let contents (file: string) = try File.ReadAllText file with _ -> ""

module EXTDATA001Tests =
    /// The demonstration investigations of causa-data the tests vendor (tests/fixtures), by id and title.
    let fixtureCases () =
        let dir = Repo.at "tests/fixtures/causa-data"
        Directory.EnumerateFiles(dir, "manifest.json", SearchOption.AllDirectories)
        |> Seq.filter (fun f -> Path.GetFileName(Path.GetDirectoryName f) |> string |> Guid.TryParse |> fst)
        |> Seq.map (fun f ->
            use doc = Text.Json.JsonDocument.Parse(File.ReadAllText f)
            let id = Path.GetFileName(Path.GetDirectoryName f) |> string
            let title =
                match doc.RootElement.TryGetProperty "title" with
                | true, t when t.ValueKind = Text.Json.JsonValueKind.String ->
                    match t.GetString() with
                    | null -> ""
                    | v -> v
                | _ -> ""
            (id: string), (title: string))
        |> List.ofSeq

    [<Fact>]
    let ``EXT-DATA-001-O1`` () =
        let cases = fixtureCases ()
        Assert.NotEmpty(cases)
        let files = Repo.shipped ()
        Assert.NotEmpty(files)
        // No shipped file holds a demonstration investigation's id, title or stored file set.
        for f in files do
            let text = Repo.contents f
            for (id, title) in cases do
                Assert.False(text.Contains id, sprintf "%s contains the demonstration investigation %s" f id)
                if title.Length > 12 then Assert.False(text.Contains title, sprintf "%s contains the demonstration title %s" f title)
            Assert.False(Path.GetFileName f = "events.ndjson", sprintf "%s is an event history" f)
        // The application starts with no investigation content: nothing is listed, loaded or held before Connect.
        let state, effects = Update.init ""
        Assert.True(state.Model.Listings.IsEmpty && state.Model.Loads.IsEmpty && state.Model.Sessions.IsEmpty)
        Assert.Empty(effects)

    [<Fact>]
    let ``EXT-DATA-001-O2`` () =
        // The contract types may ship; the synthetic fixtures may not: no application project references the test
        // projects or the fixtures, and no shipped tree holds a fixtures directory.
        for project in [ "src/Causa.Domain/Causa.Domain.fsproj"; "src/Causa.Persistence/Causa.Persistence.fsproj"; "src/Causa.App/Causa.App.fsproj"; "src/Causa.Host/Causa.Host.csproj" ] do
            let text = File.ReadAllText(Repo.at project)
            Assert.DoesNotContain("tests/", text.Replace('\\', '/'))
            Assert.DoesNotContain("fixtures", text)
        for d in [ "src"; "web"; "dist" ] |> List.map Repo.at |> List.filter Directory.Exists do
            let fixtureDirs =
                Directory.EnumerateDirectories(d, "*", SearchOption.AllDirectories)
                |> Seq.filter (fun x -> let s = x.Replace('\\', '/') in not (s.Contains "/bin/" || s.Contains "/obj/") && Path.GetFileName x = "fixtures")
                |> List.ofSeq
            Assert.Empty(fixtureDirs)
        // The storage contract itself is in the application (types and codec), so new file sets are made from it.
        Assert.Equal("causa-storage-v1.0.0", Causa.Domain.InterfaceManifest.storageContract)
