// Owner: integration. The build and its stack-lock gate (T11: VER-004, VER-006, VER-008; AR-011's build record).
//
//   dotnet fsi build.fsx [--root DIR] [--out DIR]   (`npm run build`): the gate, then the site in dist/ (or --out)
//   dotnet fsi build.fsx gate [--root DIR]          the gate alone; prints its verdict as JSON
//
// The gate is mandatory: the build never runs without it and stops unless it passes. It verifies the locked manifests
// (global.json, Directory.Packages.props, package.json, package-lock.json, .echelon records) against each other and
// against what is installed (the .NET SDK, node_modules) and the compatibility evidence the boundary depends on.
// Exit codes: 0 PASS, 1 FAIL (drift, an unpinned or unverified artifact, stale evidence), 2 BLOCKED (something missing).
//
// The site:
//   1. dotnet publish src/Causa.Host -c Release (the F# engine compiled to .NET browser-wasm, [JSExport] Dispatch)
//   2. _framework/       the published runtime and assemblies (uncompressed files only)
//   3. index.html        web/index.html with every web/screens/*.html fragment inlined at <!-- screens -->
//   4. causa.css         web/causa.css followed by every web/screens/*.css, in the same order
//   5. causa-theme.css, boundary.js, limen/ (Limen kernel ES modules), forma/ (Forma CSS)
//   6. build-info.json   the gate's verdict, the build inputs (digests) and the environment (tools, commit, CI run)
open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json
open System.Text.RegularExpressions

// ---- arguments ------------------------------------------------------------------------------------------------------
let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray
let rec option name = function
    | n :: v :: _ when n = name -> Some v
    | _ :: rest -> option name rest
    | [] -> None
let gateOnly = List.contains "gate" args
let root = option "--root" args |> Option.map Path.GetFullPath |> Option.defaultValue __SOURCE_DIRECTORY__
let at (relative: string) = Path.Combine(root, relative)
let dist = option "--out" args |> Option.map Path.GetFullPath |> Option.defaultValue (at "dist")

let run (dir: string) (file: string) (arguments: string) =
    try
        let info = ProcessStartInfo(file, arguments, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        use p = Process.Start info
        let output = p.StandardOutput.ReadToEnd()
        p.StandardError.ReadToEnd() |> ignore
        p.WaitForExit()
        p.ExitCode, output.Trim()
    with e -> -1, e.Message

let sha256 (path: string) =
    use s = File.OpenRead path
    Convert.ToHexString(SHA256.HashData s).ToLowerInvariant()

// ---- the gate -------------------------------------------------------------------------------------------------------
type Status =
    | Pass
    | Fail of string
    | Blocked of string

type Check = { Id: string; Status: Status }

exception Missing of string

let readJson (relative: string) =
    let path = at relative
    if not (File.Exists path) then raise (Missing relative)
    JsonDocument.Parse(File.ReadAllText path).RootElement

let prop (e: JsonElement) (names: string list) =
    names |> List.fold (fun (acc: JsonElement option) n ->
        acc |> Option.bind (fun v ->
            if v.ValueKind = JsonValueKind.Object then
                match v.TryGetProperty n with
                | true, x -> Some x
                | _ -> None
            else None)) (Some e)

let text (e: JsonElement) (names: string list) =
    prop e names |> Option.bind (fun v -> if v.ValueKind = JsonValueKind.String then Option.ofObj (v.GetString()) else None)

let entries (e: JsonElement option) =
    match e with
    | Some v when v.ValueKind = JsonValueKind.Object -> [ for p in v.EnumerateObject() -> p.Name, (match p.Value.GetString() with null -> "" | s -> s) ]
    | _ -> []

let exactVersion = Regex(@"^\d+\.\d+\.\d+$")
let sha40 = Regex(@"^[0-9a-f]{40}$")
let sha64 = Regex(@"^[0-9a-f]{64}$")

/// A dependency spec is pinned when it is an exact version, or an https URL of a versioned release artifact.
let pinnedSpec (spec: string) =
    exactVersion.IsMatch spec || (spec.StartsWith "https://" && Regex.IsMatch(spec, @"/v?\d+\.\d+\.\d+/") && Regex.IsMatch(spec, @"\d+\.\d+\.\d+\.tgz$"))

/// The version a spec pins (the URL's release version for a tarball).
let specVersion (spec: string) =
    if exactVersion.IsMatch spec then Some spec
    else
        let m = Regex.Match(spec, @"(\d+\.\d+\.\d+)\.tgz$")
        if m.Success then Some m.Groups[1].Value else None

let all (problems: string list) = if problems.IsEmpty then Pass else Fail(String.Join("; ", problems))

let check (id: string) (body: unit -> Status) =
    let status =
        try body () with
        | Missing what -> Blocked("missing " + what)
        | e -> Fail(e.Message)
    { Id = id; Status = status }

let packageJson () = readJson "package.json"
let directDependencies () =
    let pkg = packageJson ()
    entries (prop pkg [ "dependencies" ]) @ entries (prop pkg [ "devDependencies" ])

let lockedPackage (name: string) =
    prop (readJson "package-lock.json") [ "packages"; "node_modules/" + name ]

let gate () : Check list =
    [ check "sdk-pinned" (fun () ->
          let g = readJson "global.json"
          all [ match text g [ "sdk"; "version" ] with
                | Some v when exactVersion.IsMatch v -> ()
                | v -> yield sprintf "global.json sdk.version %A is not an exact version" v
                if text g [ "sdk"; "rollForward" ] <> Some "disable" then yield "global.json sdk.rollForward is not \"disable\"" ])

      check "sdk-installed" (fun () ->
          let pinned = text (readJson "global.json") [ "sdk"; "version" ] |> Option.defaultValue ""
          match run root "dotnet" "--version" with
          | 0, v when v = pinned -> Pass
          | 0, v -> Fail(sprintf "the .NET SDK in use is %s, global.json pins %s" v pinned)
          | _, e -> Blocked("the pinned .NET SDK " + pinned + " is not installed: " + e))

      check "nuget-pinned" (fun () ->
          let props = at "Directory.Packages.props"
          if not (File.Exists props) then raise (Missing "Directory.Packages.props")
          let versions = Regex.Matches(File.ReadAllText props, "<PackageVersion\\s+Include=\"([^\"]+)\"\\s+Version=\"([^\"]*)\"") |> Seq.map (fun m -> m.Groups[1].Value, m.Groups[2].Value) |> List.ofSeq
          all [ if versions.IsEmpty then yield "Directory.Packages.props pins no package"
                for (name, v) in versions do
                    if not (exactVersion.IsMatch v) then yield sprintf "%s is not pinned to an exact version (%s)" name v ])

      check "npm-pinned" (fun () ->
          all [ for (name, spec) in directDependencies () do
                    if not (pinnedSpec spec) then yield sprintf "package.json %s is not pinned (%s)" name spec ])

      check "npm-lock-matches-manifest" (fun () ->
          let lock = readJson "package-lock.json"
          let lockRoot = entries (prop lock [ "packages"; ""; "dependencies" ]) @ entries (prop lock [ "packages"; ""; "devDependencies" ])
          all [ if Set.ofList lockRoot <> Set.ofList (directDependencies ()) then
                    yield "package-lock.json was not resolved from this package.json (its root dependencies differ)"
                for (name, spec) in directDependencies () do
                    match lockedPackage name with
                    | None -> yield sprintf "package-lock.json does not lock %s" name
                    | Some p ->
                        if text p [ "version" ] <> specVersion spec then yield sprintf "%s is locked at %A, package.json pins %s" name (text p [ "version" ]) spec
                        if spec.StartsWith "https://" && text p [ "resolved" ] <> Some spec then yield sprintf "%s resolves to %A, not %s" name (text p [ "resolved" ]) spec ])

      check "npm-integrity" (fun () ->
          let lock = readJson "package-lock.json"
          all [ for (name, p) in (match prop lock [ "packages" ] with Some ps -> [ for x in ps.EnumerateObject() -> x.Name, x.Value ] | None -> []) do
                    if name <> "" then
                        match text p [ "integrity" ] with
                        | Some i when i.StartsWith "sha512-" -> ()
                        | _ -> yield sprintf "%s has no sha512 integrity digest" name ])

      check "npm-installed" (fun () ->
          let installed =
              [ for (name, _) in directDependencies () ->
                    let manifest = Path.Combine("node_modules", name, "package.json")
                    name, (if File.Exists(at manifest) then text (readJson manifest) [ "version" ] else None) ]
          match installed |> List.filter (snd >> Option.isNone) with
          | [] ->
              all [ for (name, v) in installed do
                        let locked = lockedPackage name |> Option.bind (fun p -> text p [ "version" ])
                        if v <> locked then yield sprintf "node_modules/%s is %A, the lock pins %A" name v locked ]
          | absent -> Blocked("not installed: " + String.Join(", ", absent |> List.map fst) + " (run npm ci)"))

      check "foundations-match-packages" (fun () ->
          let f = readJson ".echelon/foundations.json"
          let declared c = text f [ "capabilities"; c; "version" ]
          let locked name = lockedPackage name |> Option.bind (fun p -> text p [ "version" ])
          all [ for (capability, package) in [ "forma", "@echelon-foundry/design-system"; "limen", "@echelon-foundry/limen"; "folio", "@echelon-foundry/print-components" ] do
                    if declared capability <> locked package then
                        yield sprintf "foundations %s %A differs from the locked %s %A" capability (declared capability) package (locked package) ])

      check "toolchain-match-installed" (fun () ->
          let toolchain = readJson ".echelon/toolchain.json"
          let foundations = readJson ".echelon/foundations.json"
          let installed file = text (readJson file) [ "installedVersion" ]
          all [ let praxis = text toolchain [ "praxis" ]
                if praxis.IsNone || not (exactVersion.IsMatch praxis.Value) then yield "toolchain.json does not pin praxis exactly"
                if installed ".echelon/ros.json" <> praxis then yield sprintf "Praxis installed %A, toolchain pins %A" (installed ".echelon/ros.json") praxis
                if text foundations [ "capabilities"; "praxis"; "version" ] <> praxis then yield "foundations praxis differs from toolchain.json"
                let ordo = text toolchain [ "ordo" ]
                if ordo.IsNone || not (exactVersion.IsMatch ordo.Value) then yield "toolchain.json does not pin ordo exactly"
                if installed ".echelon/sde.json" <> ordo then yield sprintf "Ordo (sde) installed %A, toolchain pins %A" (installed ".echelon/sde.json") ordo
                if text foundations [ "capabilities"; "ordo"; "version" ] <> ordo then yield "foundations ordo differs from toolchain.json" ])

      check "lock-bound-to-rehearsal" (fun () ->
          let governance = readJson ".echelon/r9-governance.json"
          let rehearsal = text (readJson "ros.json") [ "name" ] |> Option.map (fun n -> Regex.Match(n, @"rehearsal-\d+$").Value) |> Option.defaultValue ""
          all [ if not (text governance [ "indyInitCommit" ] |> Option.exists sha40.IsMatch) then yield "the governing Indy-init commit is not a full SHA"
                for k in [ "policySha256"; "workflowSha256" ] do
                    if not (text governance [ k ] |> Option.exists sha64.IsMatch) then yield k + " is not a sha256 digest"
                match text governance [ "toolchainLock" ] with
                | Some lock when rehearsal <> "" && lock = sprintf "kickoff/%s/toolchain.lock.json" rehearsal -> ()
                | lock -> yield sprintf "the toolchain lock %A is not this rehearsal's (%s): re-resolve the lock for a new rehearsal" lock rehearsal ])

      check "evidence-current" (fun () ->
          let checkpoint = readJson ".echelon/r9-initialization-checkpoint.json"
          let inputs = match prop checkpoint [ "inputs" ] with Some a when a.ValueKind = JsonValueKind.Array -> [ for i in a.EnumerateArray() -> text i [ "path" ], text i [ "sha256" ] ] | _ -> []
          if inputs.IsEmpty then Fail "the initialization checkpoint records no inputs"
          else
              match inputs |> List.filter (fun (p, _) -> p.IsNone || not (File.Exists(at p.Value))) with
              | (p, _) :: _ -> Blocked(sprintf "checkpoint input %A is missing" p)
              | [] -> all [ for (p, d) in inputs do if Some(sha256 (at p.Value)) <> d then yield p.Value + " changed after the checkpoint (stale evidence)" ])

      check "boundary-compatible" (fun () ->
          let boundary = at "web/boundary.js"
          let host = at "src/Causa.Host/Host.cs"
          if not (File.Exists boundary) then raise (Missing "web/boundary.js")
          if not (File.Exists host) then raise (Missing "src/Causa.Host/Host.cs")
          let js = File.ReadAllText boundary
          let limen = Path.Combine("node_modules", "@echelon-foundry", "limen", "dist")
          if not (Directory.Exists(at limen)) then raise (Missing(limen + " (run npm ci)"))
          let imports = Regex.Matches(js, "from \"\\./limen/([^\"]+)\"") |> Seq.map (fun m -> m.Groups[1].Value) |> List.ofSeq
          let exportPath = Regex.Match(js, @"exportPath:\s*\[([^\]]*)\]").Groups[1].Value |> fun s -> Regex.Matches(s, "\"([^\"]+)\"") |> Seq.map (fun m -> m.Groups[1].Value) |> List.ofSeq
          let cs = File.ReadAllText host
          let ns = Regex.Match(cs, @"namespace\s+([\w.]+)").Groups[1].Value
          let cls = Regex.Match(cs, @"static\s+partial\s+class\s+(\w+)").Groups[1].Value
          let exported = Regex.Matches(cs, @"\[JSExport\][^;{]*?static\s+string\s+(\w+)\(") |> Seq.map (fun m -> m.Groups[1].Value) |> List.ofSeq
          all [ if imports.IsEmpty then yield "web/boundary.js imports nothing from Limen"
                for i in imports do
                    if not (File.Exists(at (Path.Combine(limen, i)))) then yield sprintf "the installed Limen has no dist/%s" i
                if not (js.Contains "DotnetWasmTransport") then yield "web/boundary.js does not use Limen's DotnetWasmTransport"
                match List.rev exportPath with
                | m :: c :: nsRev when String.Join(".", List.rev nsRev) = ns && c = cls && List.contains m exported -> ()
                | _ -> yield sprintf "boundary exportPath %A does not name a [JSExport] of %s.%s %A" exportPath ns cls exported
                if not (File.ReadAllText(at "src/Causa.Host/Causa.Host.csproj").Contains "Microsoft.NET.Sdk.WebAssembly") then
                    yield "src/Causa.Host is not a Microsoft.NET.Sdk.WebAssembly project" ]) ]

let verdict (checks: Check list) =
    if checks |> List.exists (fun c -> match c.Status with Fail _ -> true | _ -> false) then "FAIL"
    elif checks |> List.exists (fun c -> match c.Status with Blocked _ -> true | _ -> false) then "BLOCKED"
    else "PASS"

let writeGate (w: Utf8JsonWriter) (checks: Check list) =
    w.WriteStartObject()
    w.WriteString("verdict", verdict checks)
    w.WriteStartArray("checks")
    for c in checks do
        w.WriteStartObject()
        w.WriteString("id", c.Id)
        match c.Status with
        | Pass -> w.WriteString("status", "PASS")
        | Fail d -> w.WriteString("status", "FAIL"); w.WriteString("detail", d)
        | Blocked d -> w.WriteString("status", "BLOCKED"); w.WriteString("detail", d)
        w.WriteEndObject()
    w.WriteEndArray()
    w.WriteEndObject()

let toJson (write: Utf8JsonWriter -> unit) =
    use stream = new MemoryStream()
    do
        use w = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        write w
    Text.Encoding.UTF8.GetString(stream.ToArray())

let checks = gate ()
let gateVerdict = verdict checks
let exitCode = match gateVerdict with "PASS" -> 0 | "BLOCKED" -> 2 | _ -> 1

if gateOnly then
    printfn "%s" (toJson (fun w -> writeGate w checks))
    exit exitCode

for c in checks do
    match c.Status with
    | Pass -> printfn "gate %-28s PASS" c.Id
    | Fail d -> eprintfn "gate %-28s FAIL %s" c.Id d
    | Blocked d -> eprintfn "gate %-28s BLOCKED %s" c.Id d
if exitCode <> 0 then
    eprintfn "build.fsx: the stack-lock gate is %s; nothing was built." gateVerdict
    exit exitCode

// ---- the site -------------------------------------------------------------------------------------------------------
let fail (message: string) =
    eprintfn "build.fsx: %s" message
    exit 1

/// Fragments in DOM order: the shell's screens, then the workspace frame and its sections, then the rest. Any other
/// fragment follows alphabetically. A fragment not written yet is skipped.
let fragmentOrder =
    [ "investigation-list"; "investigation-start"; "workspace-frame"; "workspace-details"; "workspace-overview"
      "workspace-causal-analysis"; "workspace-evidence"; "workspace-hypotheses"; "workspace-timeline"
      "workspace-save"; "administration"; "settings"; "not-found" ]

let ordered (extension: string) =
    let dir = at "web/screens"
    let present =
        if Directory.Exists dir then Directory.GetFiles(dir, "*" + extension) |> Array.map Path.GetFileNameWithoutExtension |> Set.ofArray
        else Set.empty
    let known = fragmentOrder |> List.filter present.Contains
    let others = present |> Set.toList |> List.filter (fun n -> not (List.contains n fragmentOrder)) |> List.sort
    (known @ others) |> List.map (fun n -> Path.Combine(dir, n + extension))

let rec copyDir (source: string) (target: string) (keep: string -> bool) =
    Directory.CreateDirectory target |> ignore
    for f in Directory.GetFiles source do
        if keep f then File.Copy(f, Path.Combine(target, Path.GetFileName f), true)
    for d in Directory.GetDirectories source do
        copyDir d (Path.Combine(target, Path.GetFileName d)) keep

let relative (path: string) = Path.GetRelativePath(root, path).Replace('\\', '/')

let publishCode, publishOutput = run root "dotnet" "publish src/Causa.Host/Causa.Host.csproj -c Release -nologo"
printfn "%s" publishOutput
if publishCode <> 0 then fail "dotnet publish src/Causa.Host failed."
let framework = at "src/Causa.Host/bin/Release/net10.0/publish/wwwroot/_framework"
if not (File.Exists(Path.Combine(framework, "dotnet.js"))) then fail ("no dotnet.js in " + framework)

if Directory.Exists dist then Directory.Delete(dist, true)
Directory.CreateDirectory dist |> ignore
copyDir framework (Path.Combine(dist, "_framework")) (fun f -> not (f.EndsWith ".br" || f.EndsWith ".gz"))

let marker = "<!-- screens -->"
let shell = File.ReadAllText(at "web/index.html")
if not (shell.Contains marker) then fail "web/index.html has no <!-- screens --> marker."
let screens = ordered ".html"
let inlined =
    screens
    |> List.map (fun f -> sprintf "<!-- screen: %s -->\n%s" (Path.GetFileName f) (File.ReadAllText(f).Trim()))
    |> String.concat "\n"
File.WriteAllText(Path.Combine(dist, "index.html"), shell.Replace(marker, marker + "\n" + inlined))

let styles = at "web/causa.css" :: ordered ".css"
File.WriteAllText(Path.Combine(dist, "causa.css"), styles |> List.map File.ReadAllText |> String.concat "\n")
for name in [ "causa-theme.css"; "boundary.js"; "causa-icon.svg" ] do
    File.Copy(at ("web/" + name), Path.Combine(dist, name), true)
copyDir (at "node_modules/@echelon-foundry/limen/dist") (Path.Combine(dist, "limen")) (fun f -> f.EndsWith ".js")
copyDir (at "node_modules/@echelon-foundry/design-system/dist") (Path.Combine(dist, "forma")) (fun f -> not (f.EndsWith ".map"))
File.WriteAllText(Path.Combine(dist, ".nojekyll"), "")

// ---- the build record (AR-011) --------------------------------------------------------------------------------------
// `inputs` and `packages` are what a re-run must reproduce; `environment` is what may drift between machines. A re-run
// whose inputs match but whose output differs is an environment difference, not a source change (AR-011-O2).
let inputs =
    [ "global.json"; "Directory.Packages.props"; "Directory.Build.props"; "package.json"; "package-lock.json"
      "build.fsx"; "web/index.html"; "web/boundary.js"; "web/causa.css"; "web/causa-theme.css"; "web/causa-icon.svg" ]
    |> List.map at
    |> fun fixedInputs -> fixedInputs @ screens @ ordered ".css"
let environmentValue (name: string) = Environment.GetEnvironmentVariable name |> Option.ofObj |> Option.defaultValue ""
let record =
    toJson (fun w ->
        w.WriteStartObject()
        w.WriteString("schema", "causa.build-info/1")
        w.WriteString("commit", snd (run root "git" "rev-parse HEAD"))
        w.WritePropertyName("gate")
        writeGate w checks
        w.WriteStartArray("inputs")
        for f in inputs do
            w.WriteStartObject()
            w.WriteString("path", relative f)
            w.WriteString("sha256", sha256 f)
            w.WriteEndObject()
        w.WriteEndArray()
        w.WriteStartArray("packages")
        for p in (match prop (readJson "package-lock.json") [ "packages" ] with Some ps -> [ for x in ps.EnumerateObject() -> x ] | None -> []) do
            if p.Name <> "" then
                w.WriteStartObject()
                w.WriteString("name", p.Name.Replace("node_modules/", ""))
                w.WriteString("version", text p.Value [ "version" ] |> Option.defaultValue "")
                w.WriteString("integrity", text p.Value [ "integrity" ] |> Option.defaultValue "")
                w.WriteEndObject()
        w.WriteEndArray()
        w.WriteStartObject("environment")
        w.WriteString("dotnetSdk", snd (run root "dotnet" "--version"))
        w.WriteString("node", snd (run root "node" "--version"))
        w.WriteString("os", Runtime.InteropServices.RuntimeInformation.OSDescription)
        w.WriteString("ciRunId", environmentValue "GITHUB_RUN_ID")
        w.WriteString("ciWorkflow", environmentValue "GITHUB_WORKFLOW")
        w.WriteEndObject()
        w.WriteEndObject())
File.WriteAllText(Path.Combine(dist, "build-info.json"), record)

printfn "build.fsx: %s written (%d screen fragment(s)%s)" (relative dist) screens.Length (if screens.IsEmpty then "" else ": " + String.Join(", ", screens |> List.map Path.GetFileName))
