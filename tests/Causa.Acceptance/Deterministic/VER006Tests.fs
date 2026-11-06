// Owner: integration. T11 compatibility (VER-006): the boundary between Limen, the .NET WebAssembly host and the F#
// engine, exercised through the real engine and the real build, not only by comparing version strings.
namespace Causa.Acceptance.Deterministic

open System
open System.IO
open System.Text.Json
open Xunit
open Causa.Persistence
open Causa.App

module VER006Tests =
    open StackLockGate

    /// VER-006-O1: what the boundary depends on is compatible: boundary.js imports modules the installed Limen has and
    /// names an export the host really has; Forma, Limen and Folio as recorded are the ones locked; Praxis and Ordo as
    /// installed are the ones pinned; the deploy workflow builds and uploads what the build writes.
    [<Fact>]
    let ``VER-006-O1`` () =
        let real = run repoRoot
        for id in [ "boundary-compatible"; "foundations-match-packages"; "toolchain-match-installed" ] do
            Assert.True((status real id = "PASS"), sprintf "%s: %s" id (detail real id))
        // An export the host does not have, or a Limen without the WebAssembly transport, is incompatible.
        let renamed = judgeMutated (fun d -> editText d "src/Causa.Host/Host.cs" (fun s -> s.Replace("string Dispatch(", "string Handle(")))
        Assert.Equal("FAIL", status renamed "boundary-compatible")
        let noTransport = judgeMutated (fun d -> File.Delete(Path.Combine(d, "node_modules/@echelon-foundry/limen/dist/hosts/dotnet-wasm-transport.js")))
        Assert.Equal("FAIL", status noTransport "boundary-compatible")
        // Workflow layout: the Pages workflow runs `npm run build` and uploads dist/, which `npm run build` writes.
        let workflow = File.ReadAllText(at ".github/workflows/deploy-pages.yml")
        Assert.Contains("npm run build", workflow)
        Assert.Contains("path: dist", workflow)
        Assert.Contains("global-json-file: global.json", workflow)
        Assert.Equal("dotnet fsi build.fsx", (json "package.json").GetProperty("scripts").GetProperty("build").GetString())

    let private view (json: string) =
        use doc = JsonDocument.Parse json
        let root = doc.RootElement
        // Limen's ViewState: every value a string, number or boolean, or an array of flat rows (no nesting).
        for p in root.GetProperty("view").EnumerateObject() do
            match p.Value.ValueKind with
            | JsonValueKind.String | JsonValueKind.Number | JsonValueKind.True | JsonValueKind.False -> ()
            | JsonValueKind.Array ->
                for row in p.Value.EnumerateArray() do
                    Assert.Equal(JsonValueKind.Object, row.ValueKind)
                    for f in row.EnumerateObject() do
                        Assert.Contains(f.Value.ValueKind, [ JsonValueKind.String; JsonValueKind.Number; JsonValueKind.True; JsonValueKind.False ])
            | k -> failwithf "view key %s is a %A, outside Limen's ViewState" p.Name k
        Assert.Equal(JsonValueKind.Array, root.GetProperty("effects").ValueKind)
        Assert.Equal(JsonValueKind.Array, root.GetProperty("cancellations").ValueKind)
        root.Clone()

    let private send state (message: string) =
        let next, effects = Program.step state (Program.decode message)
        next, view (Program.encode (View.render next) effects 0)

    /// VER-006-O2: the engine answers Limen's messages, as Limen sends them, with messages Limen accepts.
    [<Fact>]
    let ``VER-006-O2`` () =
        let s1, v1 = send None """{"kind":"Initialize","protocolVersion":1,"capabilities":["Http","Storage","Clipboard","Navigation"],"location":{"path":"/","query":"","hash":"#/settings"}}"""
        let v = v1.GetProperty "view"
        Assert.True(v.GetProperty("app.started").GetBoolean())
        Assert.True(v.GetProperty("screen.settings").GetBoolean())
        Assert.False(v.GetProperty("screen.investigations").GetBoolean())
        Assert.Equal("page", v.GetProperty("nav.settings.current").GetString())
        Assert.Equal("false", v.GetProperty("nav.investigations.current").GetString())
        Assert.Equal("Settings · Causa", v.GetProperty("app.title").GetString())
        // Back/Forward and followed hash links arrive as LocationChanged.
        let s2, v2 = send (Some s1) """{"kind":"LocationChanged","location":{"path":"/","query":"","hash":"#/investigation/workspaces%2Fdemo%2Fr10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/evidence"}}"""
        let v = v2.GetProperty "view"
        Assert.True(v.GetProperty("screen.investigation").GetBoolean())
        Assert.True(v.GetProperty("screen.investigation.evidence").GetBoolean())
        Assert.Equal("workspaces/demo/r10", v.GetProperty("route.workspace").GetString())
        Assert.Equal("page", v.GetProperty("nav.investigations.current").GetString())
        Assert.Equal(Route "#/investigation/workspaces%2Fdemo%2Fr10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/evidence", s2.Model.Route)
        // A navigation the engine performs is requested as a Limen Navigation effect.
        let s3, v3 = send (Some s2) """{"kind":"Event","event":{"kind":"Event","name":"app.navigate","value":"#/administration"}}"""
        Assert.True(v3.GetProperty("view").GetProperty("screen.administration").GetBoolean())
        let effect = v3.GetProperty("effects").EnumerateArray() |> Seq.exactlyOne
        Assert.Equal("Navigation", effect.GetProperty("kind").GetString())
        Assert.Equal("push", effect.GetProperty("operation").GetString())
        Assert.Equal("#/administration", effect.GetProperty("url").GetString())
        // A field edit is kept as a draft; a credential never reaches the view.
        let s4, _ = send (Some s3) """{"kind":"Event","event":{"kind":"Event","name":"new.title","value":"Pump failure"}}"""
        Assert.Equal("Pump failure", Causa.App.Ui.Drafts.get s4 "new.title")
        let s5, v5 = send (Some s4) """{"kind":"Event","event":{"kind":"Event","name":"connection.token","value":"ghp_secret-value"}}"""
        Assert.True(s5.Credential.IsSome)
        Assert.DoesNotContain("ghp_secret-value", v5.GetRawText())
        Assert.False(s5.Drafts.ContainsKey "connection.token")
        // An unknown route is not found; an unrecognised message changes nothing.
        let s6, v6 = send (Some s5) """{"kind":"LocationChanged","location":{"path":"/","query":"","hash":"#/nowhere"}}"""
        Assert.True(v6.GetProperty("view").GetProperty("screen.not-found").GetBoolean())
        let s7, _ = send (Some s6) """{"kind":"CapabilityFact","capability":"x","version":1,"fact":null}"""
        Assert.Equal(s6.Route, s7.Route)
        // A Limen Http result reaches the engine as the frozen IF41-WIRE outcome, body untouched.
        match Program.decode """{"kind":"EffectResult","result":{"kind":"HttpResult","correlationId":"http-7","outcome":{"kind":"Success","status":200,"body":"{\"sha\":\"abc\"}"}}}""" with
        | Program.HttpResult(Answered(RequestId 7L, 200, [], "{\"sha\":\"abc\"}")) -> ()
        | other -> failwithf "unexpected %A" other
        match Program.decode """{"kind":"EffectResult","result":{"kind":"HttpResult","correlationId":"http-8","outcome":{"kind":"OutcomeUnknown","reason":"connection-lost"}}}""" with
        | Program.HttpResult(NotAnswered(RequestId 8L, LostAfterSending)) -> ()
        | other -> failwithf "unexpected %A" other

    /// VER-006-O3: the local bootstrap/build path: `dotnet fsi build.fsx` (what `npm run build` runs) passes its gate,
    /// publishes the WebAssembly engine and assembles the static site the browser loads.
    [<Fact>]
    let ``VER-006-O3`` () =
        let out = Path.Combine(Path.GetTempPath(), "causa-site-" + Guid.NewGuid().ToString("N"))
        try
            let code, output = runDotnet repoRoot (sprintf "fsi build.fsx --out \"%s\"" out) 600000
            Assert.True((code = 0), output)
            let index = File.ReadAllText(Path.Combine(out, "index.html"))
            Assert.Contains("<!-- screens -->", index)
            Assert.Contains("src=\"boundary.js\"", index)
            Assert.Contains("Loading Causa…", index)
            Assert.Contains("data-bind-hidden=\"app.started\"", index)
            let boundary = File.ReadAllText(Path.Combine(out, "boundary.js"))
            Assert.Contains("./_framework/dotnet.js", boundary)
            Assert.True(File.Exists(Path.Combine(out, "_framework", "dotnet.js")))
            let framework = Directory.GetFiles(Path.Combine(out, "_framework")) |> Array.map (Path.GetFileName >> required "file name")
            for assembly in [ "Causa.Host"; "Causa.App"; "Causa.Domain"; "Causa.Persistence"; "FSharp.Core" ] do
                Assert.True(framework |> Array.exists (fun f -> f.StartsWith(assembly + ".") && f.EndsWith ".wasm"), assembly + " is not published")
            for f in [ "limen/index.js"; "limen/hosts/dotnet-wasm-transport.js"; "forma/all.css"; "causa.css"; "causa-theme.css" ] do
                Assert.True(File.Exists(Path.Combine(out, f)), f)
            use info = JsonDocument.Parse(File.ReadAllText(Path.Combine(out, "build-info.json")))
            Assert.Equal("PASS", info.RootElement.GetProperty("gate").GetProperty("verdict").GetString())
        finally
            remove out
