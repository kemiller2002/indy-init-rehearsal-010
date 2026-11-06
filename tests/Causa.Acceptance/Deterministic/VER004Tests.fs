// Owner: integration. T11 stack lock (VER-004). The gate under test is the build's own mandatory gate
// (`dotnet fsi build.fsx gate`), run against the real repository and against copies with one manifest changed.
namespace Causa.Acceptance.Deterministic

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open Xunit

/// Runs the repository's stack-lock gate and builds the copies the negative cases mutate. Shared by VER004/006/008.
module StackLockGate =
    /// The repository root: the nearest ancestor of the test assembly that holds Causa.slnx.
    let repoRoot =
        let rec up (dir: DirectoryInfo | null) =
            match dir with
            | null -> failwith "no Causa.slnx above the test assembly"
            | d when File.Exists(Path.Combine(d.FullName, "Causa.slnx")) -> d.FullName
            | d -> up d.Parent
        up (DirectoryInfo AppContext.BaseDirectory)

    /// A value the API may report as null, required here.
    let required (what: string) (value: 'a | null) : 'a =
        match value with
        | null -> failwithf "%s is missing" what
        | v -> v

    /// A JSON string property's text ("" when absent or null).
    let str (e: JsonElement) =
        match e.GetString() with
        | null -> ""
        | s -> s

    let at (relative: string) = Path.Combine(repoRoot, relative)

    let private dotnet =
        match Environment.GetEnvironmentVariable "DOTNET_HOST_PATH" with
        | null | "" -> "dotnet"
        | path -> path

    /// Runs `dotnet <arguments>` in a directory; exit code and standard output.
    let runDotnet (dir: string) (arguments: string) (timeoutMs: int) =
        let info = ProcessStartInfo(dotnet, arguments, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        use p = Process.Start info |> required ("dotnet " + arguments)
        let out = p.StandardOutput.ReadToEndAsync()
        let err = p.StandardError.ReadToEndAsync()
        if not (p.WaitForExit timeoutMs) then
            p.Kill true
            failwithf "dotnet %s did not finish in %d ms" arguments timeoutMs
        p.WaitForExit()
        p.ExitCode, out.Result + err.Result

    type GateResult = { ExitCode: int; Verdict: string; Checks: Map<string, string * string> }

    /// `dotnet fsi build.fsx gate --root <root>`: the real repository's build.fsx, started in the repository (its pinned
    /// SDK), judging whichever tree `root` names.
    let run (root: string) : GateResult =
        let code, output = runDotnet repoRoot (sprintf "fsi \"%s\" gate --root \"%s\"" (at "build.fsx") root) 120000
        let start = output.IndexOf '{'
        if start < 0 then failwithf "the gate printed no verdict (exit %d): %s" code output
        use doc = JsonDocument.Parse(output.Substring start)
        let r = doc.RootElement
        { ExitCode = code
          Verdict = str (r.GetProperty "verdict")
          Checks =
            [ for c in r.GetProperty("checks").EnumerateArray() ->
                  let detail = match c.TryGetProperty "detail" with | true, d -> str d | _ -> ""
                  str (c.GetProperty "id"), (str (c.GetProperty "status"), detail) ]
            |> Map.ofList }

    let status (result: GateResult) (id: string) =
        match Map.tryFind id result.Checks with
        | Some(s, _) -> s
        | None -> failwithf "the gate has no check %s" id

    let detail (result: GateResult) (id: string) = Map.tryFind id result.Checks |> Option.map snd |> Option.defaultValue ""

    let private copyFile (source: string) (target: string) =
        Directory.CreateDirectory(Path.GetDirectoryName target |> required target) |> ignore
        File.Copy(source, target, true)

    let rec private copyDir (source: string) (target: string) =
        Directory.CreateDirectory target |> ignore
        for f in Directory.GetFiles source do File.Copy(f, Path.Combine(target, Path.GetFileName f |> required f), true)
        for d in Directory.GetDirectories source do copyDir d (Path.Combine(target, Path.GetFileName d |> required d))

    /// A copy of every file the gate reads, in a fresh directory: the manifests, the .echelon records, the checkpoint's
    /// inputs, the boundary sources, the installed packages' manifests and the Limen modules the boundary imports.
    let copy () : string =
        let dir = Path.Combine(Path.GetTempPath(), "causa-gate-" + Guid.NewGuid().ToString("N"))
        let files =
            [ "global.json"; "Directory.Packages.props"; "package.json"; "package-lock.json"; "ros.json"
              "web/boundary.js"; "src/Causa.Host/Host.cs"; "src/Causa.Host/Causa.Host.csproj" ]
            @ [ for f in Directory.GetFiles(at ".echelon", "*.json") -> Path.GetRelativePath(repoRoot, f) ]
        for f in files do copyFile (at f) (Path.Combine(dir, f))
        use checkpoint = JsonDocument.Parse(File.ReadAllText(at ".echelon/r9-initialization-checkpoint.json"))
        for i in checkpoint.RootElement.GetProperty("inputs").EnumerateArray() do
            let p = str (i.GetProperty "path")
            copyFile (at p) (Path.Combine(dir, p))
        use pkg = JsonDocument.Parse(File.ReadAllText(at "package.json"))
        for section in [ "dependencies"; "devDependencies" ] do
            for d in pkg.RootElement.GetProperty(section).EnumerateObject() do
                let manifest = Path.Combine("node_modules", d.Name, "package.json")
                copyFile (at manifest) (Path.Combine(dir, manifest))
        copyDir (at "node_modules/@echelon-foundry/limen/dist") (Path.Combine(dir, "node_modules/@echelon-foundry/limen/dist"))
        dir

    /// The node at a path of property names.
    let rec nodeAt (path: string list) (n: JsonNode) : JsonNode =
        match path with
        | [] -> n
        | key :: rest -> nodeAt rest (required key n[key])

    /// Sets the property at a path.
    let set (path: string list) (value: string) (n: JsonNode) =
        let parent = nodeAt (List.take (path.Length - 1) path) n
        parent[List.last path] <- JsonValue.Create value

    /// Removes the property at a path.
    let unset (path: string list) (n: JsonNode) =
        (nodeAt (List.take (path.Length - 1) path) n).AsObject().Remove(List.last path) |> ignore

    /// Rewrites one JSON file of a copy.
    let editJson (dir: string) (relative: string) (edit: JsonNode -> unit) =
        let path = Path.Combine(dir, relative)
        let node = JsonNode.Parse(File.ReadAllText path) |> required relative
        edit node
        File.WriteAllText(path, node.ToJsonString())

    let editText (dir: string) (relative: string) (edit: string -> string) =
        let path = Path.Combine(dir, relative)
        File.WriteAllText(path, edit (File.ReadAllText path))

    let remove (dir: string) = try Directory.Delete(dir, true) with _ -> ()

    /// A copy, mutated, judged by the gate, then removed.
    let judgeMutated (mutate: string -> unit) : GateResult =
        let dir = copy ()
        try
            mutate dir
            run dir
        finally
            remove dir

    let json (relative: string) = JsonDocument.Parse(File.ReadAllText(at relative)).RootElement

module VER004Tests =
    open StackLockGate

    let private exact = Regex(@"^\d+\.\d+\.\d+$")

    /// VER-004-O1: exact package versions and immutable digests/commit SHAs in machine-readable manifests.
    [<Fact>]
    let ``VER-004-O1`` () =
        // The manifests as committed.
        let sdk = json "global.json"
        Assert.Matches(exact, sdk.GetProperty("sdk").GetProperty("version").GetString())
        Assert.Equal("disable", sdk.GetProperty("sdk").GetProperty("rollForward").GetString())
        let props = File.ReadAllText(at "Directory.Packages.props")
        let versions = Regex.Matches(props, "<PackageVersion\\s+Include=\"([^\"]+)\"\\s+Version=\"([^\"]*)\"") |> List.ofSeq
        Assert.NotEmpty versions
        for m in versions do Assert.Matches(exact, m.Groups[2].Value)
        let lock = json "package-lock.json"
        for p in lock.GetProperty("packages").EnumerateObject() do
            if p.Name <> "" then
                Assert.StartsWith("sha512-", p.Value.GetProperty("integrity").GetString())
                Assert.Matches(exact, p.Value.GetProperty("version").GetString())
        Assert.Matches("^[0-9a-f]{40}$", (json ".echelon/r9-governance.json").GetProperty("indyInitCommit").GetString())
        // FSharp.Core is pinned centrally to exactly the version the pinned SDK bundles, and every Causa F# project
        // references it (central package management disables the SDK's implicit reference; without this no F# test
        // project or the WebAssembly host ships FSharp.Core.dll).
        let fsharpCore = Regex.Match(props, "<PackageVersion\\s+Include=\"FSharp.Core\"\\s+Version=\"([^\"]+)\"").Groups[1].Value
        Assert.Matches(exact, fsharpCore)
        let pinnedSdk = str (sdk.GetProperty("sdk").GetProperty "version")
        let _, sdks = runDotnet repoRoot "--list-sdks" 60000
        let sdkLine = sdks.Split('\n') |> Array.find (fun l -> l.StartsWith(pinnedSdk + " ["))
        let sdkDir = Path.Combine(sdkLine.Substring(sdkLine.IndexOf('[') + 1).TrimEnd(']', '\r', ' '), pinnedSdk)
        Assert.True(File.Exists(Path.Combine(sdkDir, "FSharp", "library-packs", sprintf "FSharp.Core.%s.nupkg" fsharpCore)),
                    sprintf "SDK %s does not bundle FSharp.Core %s" pinnedSdk fsharpCore)
        Assert.Contains("<PackageReference Include=\"FSharp.Core\" />", File.ReadAllText(at "Directory.Build.props"))
        // The gate agrees on the real repository...
        let real = run repoRoot
        for id in [ "sdk-pinned"; "nuget-pinned"; "npm-pinned"; "npm-integrity" ] do
            Assert.True((status real id = "PASS"), sprintf "%s: %s" id (detail real id))
        // ...and refuses a range, a floating SDK and a missing digest.
        let ranged = judgeMutated (fun d -> editJson d "package.json" (set [ "dependencies"; "@echelon-foundry/limen" ] "^0.9.1"))
        Assert.Equal("FAIL", status ranged "npm-pinned")
        Assert.Equal("FAIL", ranged.Verdict)
        let floating = judgeMutated (fun d -> editJson d "global.json" (set [ "sdk"; "rollForward" ] "latestPatch"))
        Assert.Equal("FAIL", status floating "sdk-pinned")
        let undigested = judgeMutated (fun d -> editJson d "package-lock.json" (unset [ "packages"; "node_modules/typescript"; "integrity" ]))
        Assert.Equal("FAIL", status undigested "npm-integrity")
        let nugetRange = judgeMutated (fun d -> editText d "Directory.Packages.props" (fun s -> s.Replace("Version=\"2.9.2\"", "Version=\"[2.9,3.0)\"")))
        Assert.Equal("FAIL", status nugetRange "nuget-pinned")

    /// VER-004-O2: the lock is fixed for the run: the manifests agree with each other and with what is installed, and
    /// any drift between them fails.
    [<Fact>]
    let ``VER-004-O2`` () =
        let real = run repoRoot
        for id in [ "sdk-installed"; "npm-lock-matches-manifest"; "npm-installed"; "foundations-match-packages"; "toolchain-match-installed" ] do
            Assert.True((status real id = "PASS"), sprintf "%s: %s" id (detail real id))
        // Installed drift: a different Limen in node_modules than the lock pins.
        let installedDrift =
            judgeMutated (fun d -> editJson d "node_modules/@echelon-foundry/limen/package.json" (set [ "version" ] "0.9.2"))
        Assert.Equal("FAIL", status installedDrift "npm-installed")
        Assert.Equal("FAIL", installedDrift.Verdict)
        // Lock drift: package-lock.json re-resolved without package.json.
        let lockDrift =
            judgeMutated (fun d -> editJson d "package-lock.json" (set [ "packages"; "node_modules/@echelon-foundry/limen"; "version" ] "0.9.2"))
        Assert.Equal("FAIL", status lockDrift "npm-lock-matches-manifest")
        // Record drift: the foundations record claims another Forma than the one locked.
        let recordDrift =
            judgeMutated (fun d -> editJson d ".echelon/foundations.json" (set [ "capabilities"; "forma"; "version" ] "0.12.0"))
        Assert.Equal("FAIL", status recordDrift "foundations-match-packages")
        // Toolchain drift: the installed Praxis is not the pinned one.
        let toolDrift = judgeMutated (fun d -> editJson d ".echelon/ros.json" (set [ "installedVersion" ] "3.26.0"))
        Assert.Equal("FAIL", status toolDrift "toolchain-match-installed")
        // SDK drift: global.json pins an SDK that is not the one in use.
        let sdkDrift = judgeMutated (fun d -> editJson d "global.json" (set [ "sdk"; "version" ] "10.0.999"))
        Assert.NotEqual<string>("PASS", status sdkDrift "sdk-installed")
        Assert.NotEqual<string>("PASS", sdkDrift.Verdict)

    /// VER-004-O3: the lock in force is the one resolved for this rehearsal; a new rehearsal must re-resolve it.
    [<Fact>]
    let ``VER-004-O3`` () =
        let real = run repoRoot
        Assert.True((status real "lock-bound-to-rehearsal" = "PASS"), detail real "lock-bound-to-rehearsal")
        let rehearsal = Regex.Match(str ((json "ros.json").GetProperty "name"), @"rehearsal-\d+$").Value
        Assert.Equal(sprintf "kickoff/%s/toolchain.lock.json" rehearsal, (json ".echelon/r9-governance.json").GetProperty("toolchainLock").GetString())
        // The next rehearsal, still carrying this rehearsal's lock, is refused until the lock is re-resolved.
        let nextRun = judgeMutated (fun d -> editJson d "ros.json" (set [ "name" ] "indy-init-rehearsal-999"))
        Assert.Equal("FAIL", status nextRun "lock-bound-to-rehearsal")
        Assert.Contains("re-resolve", detail nextRun "lock-bound-to-rehearsal")
        // An unpinned governing commit is refused as well.
        let unpinned = judgeMutated (fun d -> editJson d ".echelon/r9-governance.json" (set [ "indyInitCommit" ] "main"))
        Assert.Equal("FAIL", status unpinned "lock-bound-to-rehearsal")
