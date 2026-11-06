// Owner: integration (written by ui-2 under CR-T29-TESTS, approved by integration authority). VER-007: resolved
// application, dependency and build versions are shown in Administration (UI-018) and nowhere else, and they are the
// versions actually loaded (the running runtime, the loaded domain assembly, the installed packages the build recorded),
// not numbers copied from requirements.
namespace Causa.Acceptance.Deterministic

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open Xunit
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui
open Causa.App.Views

module VER007Tests =
    open StackLockGate

    let private commit = "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c"

    /// The build record as build.fsx writes it, from the repository's installed packages (node_modules/<name>/package.json):
    /// what a build of this checkout records and serves as dist/build-info.json.
    let private buildRecordOfInstalled (verdict: string) =
        let lockPackages =
            use lock = JsonDocument.Parse(File.ReadAllText(at "package-lock.json"))
            [ for p in lock.RootElement.GetProperty("packages").EnumerateObject() do
                  if p.Name.StartsWith "node_modules/" then
                      match p.Value.TryGetProperty "version" with
                      | true, v -> yield p.Name.Substring("node_modules/".Length), v.GetString() |> nonNull
                      | _ -> () ]
        let packages = JsonArray()
        for name, version in lockPackages do
            let o = JsonObject()
            o["name"] <- JsonValue.Create(name)
            o["version"] <- JsonValue.Create(version)
            o["integrity"] <- JsonValue.Create("")
            packages.Add(o)
        let gate = JsonObject()
        gate["verdict"] <- JsonValue.Create(verdict)
        gate["checks"] <- JsonArray()
        let root = JsonObject()
        root["schema"] <- JsonValue.Create("causa.build-info/1")
        root["commit"] <- JsonValue.Create(commit)
        root["gate"] <- gate
        root["inputs"] <- JsonArray()
        root["packages"] <- packages
        root.ToJsonString()

    /// The version node_modules actually holds for a package (what the build bundled).
    let private installed (name: string) =
        use pkg = JsonDocument.Parse(File.ReadAllText(at (Path.Combine("node_modules", name, "package.json"))))
        pkg.RootElement.GetProperty("version").GetString() |> nonNull

    let private state (hash: string) =
        let model = { Update.initialModel (Routes.route hash) with Connection = Connected true; Context = Some { Repository = { Owner = "harness"; Name = "causa-test"; Branch = "main" }; Credential = Credential.create "secret-token-value" } }
        { Model = model; Route = UiRoutes.parse hash; Drafts = Map.empty; Credential = Some(Credential.create "secret-token-value"); Announcements = [] }

    let private factsOf (view: (string * ViewValue) list) =
        match view |> List.find (fst >> (=) "administration.facts") |> snd with
        | BindingValue.Items rows ->
            rows
            |> List.map (fun r ->
                let t f = match r.[f] with BindingPrimitive.Text s -> s | p -> failwithf "%A" p
                t "key", (t "label", t "value", t "status"))
            |> Map.ofList
        | other -> failwithf "%A" other

    let private texts (view: (string * ViewValue) list) =
        view
        |> List.collect (fun (_, v) ->
            match v with
            | BindingValue.Text s -> [ s ]
            | BindingValue.Items rows -> rows |> List.collect (fun r -> r |> Map.toList |> List.choose (function _, BindingPrimitive.Text s -> Some s | _ -> None))
            | _ -> [])

    /// VER-007-O1: the resolved application, dependency and build versions are in Administration; investigation pages,
    /// the investigation list (catalog cards), Settings and the shell's global regions never show them.
    [<Fact>]
    let ``VER-007-O1`` () =
        let record = buildRecordOfInstalled "PASS"
        let admin = Administration.viewWith (Some(Ok record)) (state "#/administration")
        let facts = factsOf admin
        let versions =
            [ "Causa build " + commit.Substring(0, 7)
              commit
              installed "@echelon-foundry/limen"
              installed "@echelon-foundry/design-system"
              $".NET {Environment.Version}"
              Causa.Domain.InterfaceManifest.storageContract ]
        let shown = texts admin
        for v in versions do
            Assert.True(shown |> List.exists (fun s -> s.Contains v), $"Administration shows {v}")
        Assert.Equal(("Application version", "Causa build " + commit.Substring(0, 7), "Known"), facts.["application-version"])
        Assert.Equal(("Deployed commit", commit, "Known"), facts.["deployed-commit"])
        // Every other page the application renders, shell included, with the same tab state: no version appears.
        let elsewhere =
            [ "#/investigations"; "#/new"; "#/settings"; "#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview"; "#/no-such-page" ]
        for hash in elsewhere do
            let rendered = View.render (state hash) |> List.filter (fun (k, _) -> not (k.StartsWith "administration."))
            let all = texts rendered
            for v in versions do
                Assert.False(all |> List.exists (fun s -> s.Contains v), $"{hash} shows the version {v}")
        // Administration itself is not where investigation content or the credential appears.
        Assert.False(shown |> List.exists (fun s -> s.Contains "secret-token-value"), "the token is never shown")

    /// VER-007-O2: the versions are the loaded ones: the components as installed and recorded by the build, the runtime
    /// this code runs on, the contract constants of the loaded domain assembly; an unread or broken build record yields
    /// Unknown or Failed, never a version taken from a requirement.
    [<Fact>]
    let ``VER-007-O2`` () =
        let facts = factsOf (Administration.viewWith (Some(Ok(buildRecordOfInstalled "PASS"))) (state "#/administration"))
        let value key = let _, v, _ = facts.[key] in v
        let word key = let _, _, w = facts.[key] in w
        Assert.Equal(installed "@echelon-foundry/limen", value "component-limen")
        Assert.Equal(installed "@echelon-foundry/design-system", value "component-forma")
        Assert.Equal(installed "@echelon-foundry/print-components", value "component-folio")
        Assert.Equal($".NET {Environment.Version}", value "runtime")
        Assert.Equal(Causa.Domain.InterfaceManifest.storageContract, value "storage-contract")
        Assert.Equal("Verified", word "build-gate")
        // The served build record: when this checkout has been built, the record build.fsx wrote parses and names the
        // checkout's installed component versions.
        let served = at (Path.Combine("dist", "build-info.json"))
        if File.Exists served then
            match Administration.parseBuild (File.ReadAllText served) with
            | Ok b ->
                for name in [ "@echelon-foundry/limen"; "@echelon-foundry/design-system" ] do
                    Assert.Equal(Some(installed name), b.Packages |> List.tryFind (fst >> (=) name) |> Option.map snd)
            | Error why -> failwithf "dist/build-info.json does not parse: %s" why
        // Not read yet: Unknown. A failed gate: Failed. A missing or broken record: Failed with the reason. Never a number.
        let unread = factsOf (Administration.viewWith None (state "#/administration"))
        for key in [ "application-version"; "deployed-commit"; "component-limen" ] do
            let _, _, w = unread.[key]
            Assert.Equal("Unknown", w)
        let failedGate = factsOf (Administration.viewWith (Some(Ok(buildRecordOfInstalled "FAIL"))) (state "#/administration"))
        let _, _, gateWord = failedGate.["build-gate"]
        Assert.Equal("Failed", gateWord)
        for broken in [ Error "HTTP 404"; Ok "{ not json"; Ok """{"schema":"something-else/1","commit":"abc"}""" ] do
            let f = factsOf (Administration.viewWith (Some broken) (state "#/administration"))
            let _, v, w = f.["application-version"]
            Assert.Equal("Failed", w)
            Assert.DoesNotMatch(@"\d+\.\d+\.\d+", v)
