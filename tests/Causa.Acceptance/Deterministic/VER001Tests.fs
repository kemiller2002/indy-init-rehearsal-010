// Owner: integration. T10 release baseline (VER-001). Every check reads the real repository: the Conditor lock
// (.conditor/lock.json) is the locked Echelon release set, and the manifests the build actually consumes must agree
// with it. The helper module is shared by VER002/003/005.
namespace Causa.Acceptance.Deterministic

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Xml.Linq
open Xunit

/// Reads the repository's release-baseline records: the Conditor lock and every manifest that pins an Echelon system.
module ReleaseBaseline =
    /// The repository root: the nearest ancestor of the test assembly that holds Causa.slnx.
    let repoRoot =
        let rec up (dir: DirectoryInfo | null) =
            match dir with
            | null -> failwith "no Causa.slnx above the test assembly"
            | d when File.Exists(Path.Combine(d.FullName, "Causa.slnx")) -> d.FullName
            | d -> up d.Parent
        up (DirectoryInfo AppContext.BaseDirectory)

    let at (relative: string) = Path.Combine(repoRoot, relative)
    let text (relative: string) = File.ReadAllText(at relative)
    let json (relative: string) = JsonDocument.Parse(text relative).RootElement

    let str (e: JsonElement) =
        match e.ValueKind with
        | JsonValueKind.String ->
            match e.GetString() with
            | null -> ""
            | s -> s
        | _ -> ""

    let prop (name: string) (e: JsonElement) =
        match e.ValueKind with
        | JsonValueKind.Object ->
            match e.TryGetProperty name with
            | true, v -> Some v
            | _ -> None
        | _ -> None

    let propStr (name: string) (e: JsonElement) = prop name e |> Option.map str |> Option.defaultValue ""
    let props (e: JsonElement) = e.EnumerateObject() |> Seq.map (fun p -> p.Name, p.Value) |> List.ofSeq
    let items (e: JsonElement) = e.EnumerateArray() |> List.ofSeq

    /// An exact stable SemVer: MAJOR.MINOR.PATCH with no pre-release or build suffix.
    let isStable (version: string) = Regex.IsMatch(version, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
    let isSha256 (s: string) = Regex.IsMatch(s, "^[0-9a-f]{64}$")
    let isCommit (s: string) = Regex.IsMatch(s, "^[0-9a-f]{40}$")

    type LockedComponent =
        { Id: string
          Version: string
          Distribution: string
          Package: string
          DescriptorSha256: string
          Authority: string
          SourceReference: string }

    let lockFile = ".conditor/lock.json"
    let lock () = json lockFile

    let lockedComponents () =
        lock ()
        |> prop "components"
        |> Option.map items
        |> Option.defaultValue []
        |> List.map (fun c ->
            { Id = propStr "id" c
              Version = propStr "version" c
              Distribution = propStr "distribution" c
              Package = propStr "package" c
              DescriptorSha256 = propStr "descriptorSha256" c
              Authority = propStr "authority" c
              SourceReference = propStr "sourceReference" c })

    let lockedVersion (id: string) =
        lockedComponents () |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> c.Version)

    /// The components the lock's embedded manifest declares (id, version).
    let manifestComponents () =
        lock ()
        |> prop "manifest"
        |> Option.bind (prop "components")
        |> Option.map items
        |> Option.defaultValue []
        |> List.map (fun c -> propStr "id" c, propStr "version" c)

    /// package.json dependencies and devDependencies: (section, name, spec).
    let packageJsonDeps () =
        let pkg = json "package.json"
        [ "dependencies"; "devDependencies" ]
        |> List.collect (fun section ->
            prop section pkg
            |> Option.map props
            |> Option.defaultValue []
            |> List.map (fun (name, spec) -> section, name, str spec))

    let isEchelon (name: string) = name.StartsWith("@echelon-foundry/", StringComparison.Ordinal)

    type LockedPackage = { Name: string; Version: string; Resolved: string; Integrity: string; IsLink: bool }

    /// package-lock.json's installed packages (every node_modules entry).
    let installedPackages () =
        json "package-lock.json"
        |> prop "packages"
        |> Option.map props
        |> Option.defaultValue []
        |> List.filter (fun (key, _) -> key.StartsWith("node_modules/", StringComparison.Ordinal))
        |> List.map (fun (key, v) ->
            { Name = key.Substring(key.LastIndexOf "node_modules/" + "node_modules/".Length)
              Version = propStr "version" v
              Resolved = propStr "resolved" v
              Integrity = propStr "integrity" v
              IsLink = prop "link" v |> Option.exists (fun l -> l.ValueKind = JsonValueKind.True) })

    /// The package-lock entries the root manifest depends on directly (top-level node_modules/<name>).
    let directInstalled () =
        let direct = packageJsonDeps () |> List.map (fun (_, n, _) -> n) |> Set.ofList
        installedPackages () |> List.filter (fun p -> direct.Contains p.Name)

    /// The release version a package.json spec names: an exact version, or the version of a GitHub release asset.
    let specVersion (spec: string) =
        let m = Regex.Match(spec, @"^https://github\.com/[^/]+/[^/]+/releases/download/v([^/]+)/[^/]+-\1\.tgz$")
        if m.Success then Some m.Groups.[1].Value
        elif Regex.IsMatch(spec, @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$") then Some spec
        else None

    /// Central NuGet pins (Directory.Packages.props): (package, version).
    let nugetPins () =
        XDocument.Load(at "Directory.Packages.props").Descendants(XName.Get "PackageVersion")
        |> Seq.map (fun e ->
            let attr (n: string) =
                match e.Attribute(XName.Get n) with
                | null -> ""
                | a -> a.Value
            attr "Include", attr "Version")
        |> List.ofSeq

    let workflows () =
        Directory.GetFiles(at ".github/workflows", "*.yml")
        |> Array.sort
        |> Array.map (fun f -> Path.GetFileName f, File.ReadAllText f)
        |> List.ofArray

    let sha256File (relative: string) =
        use s = File.OpenRead(at relative)
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData s).ToLowerInvariant()

    /// Every place in the repository that pins an Echelon system, paired with the locked version it must equal:
    /// (where, component, pinned, locked). The build consumes these, so each must be the locked release.
    let echelonPins () =
        let locked = lockedComponents ()
        let lockedOf id = locked |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> c.Version) |> Option.defaultValue "<not locked>"
        let byPackage = locked |> List.map (fun c -> c.Package, c) |> Map.ofList
        let manifest = manifestComponents () |> List.map (fun (id, v) -> "lock manifest", id, v, lockedOf id)
        let foundations =
            json ".echelon/foundations.json"
            |> prop "capabilities"
            |> Option.map props
            |> Option.defaultValue []
            |> List.choose (fun (id, cap) -> prop "version" cap |> Option.map (fun v -> ".echelon/foundations.json", id, str v, lockedOf id))
        let toolchain =
            json ".echelon/toolchain.json"
            |> props
            |> List.filter (fun (k, _) -> k <> "schemaVersion")
            |> List.map (fun (id, v) -> ".echelon/toolchain.json", id, str v, lockedOf id)
        let ros = [ ".ros/installation.json", "praxis", propStr "package_version" (json ".ros/installation.json"), lockedOf "praxis" ]
        let percepta = [ ".echelon/percepta.json", "percepta", propStr "installedVersion" (json ".echelon/percepta.json"), lockedOf "percepta" ]
        let npmSpecs =
            packageJsonDeps ()
            |> List.filter (fun (_, n, _) -> isEchelon n)
            |> List.map (fun (_, n, spec) ->
                let id = byPackage.TryFind n |> Option.map (fun c -> c.Id) |> Option.defaultValue n
                "package.json", id, (specVersion spec |> Option.defaultValue spec), lockedOf id)
        let npmInstalled =
            directInstalled ()
            |> List.filter (fun p -> isEchelon p.Name)
            |> List.map (fun p ->
                let id = byPackage.TryFind p.Name |> Option.map (fun c -> c.Id) |> Option.defaultValue p.Name
                "package-lock.json", id, p.Version, lockedOf id)
        manifest @ foundations @ toolchain @ ros @ percepta @ npmSpecs @ npmInstalled

    let show (items: string list) = String.Join("; ", items)

module VER001Tests =
    open ReleaseBaseline

    /// The systems the requirement names that this application integrates (Dokimos and Forma Studio are verification
    /// and authoring tools, pinned by the governed toolchain lock, not components of the application build).
    let integratedSystems = [ "praxis"; "ordo"; "percepta"; "forma"; "limen"; "folio" ]

    [<Fact>]
    let ``VER-001-O1`` () =
        let locked = lockedComponents ()
        Assert.NotEmpty locked
        let unstable = locked |> List.filter (fun c -> not (isStable c.Version)) |> List.map (fun c -> c.Id + "@" + c.Version)
        Assert.True(unstable.IsEmpty, "locked components that are not an exact stable release: " + show unstable)
        let pins = echelonPins ()
        // Each locked system is consumed somewhere besides the lock itself, so the comparison is not vacuous.
        for c in locked do
            Assert.True(pins |> List.exists (fun (_, id, _, _) -> id = c.Id), c.Id + " is locked but its pinned version is never compared")
        let drift = pins |> List.filter (fun (_, _, pinned, lockedV) -> pinned <> lockedV) |> List.map (fun (w, id, p, l) -> sprintf "%s pins %s@%s, lock %s" w id p l)
        Assert.True(drift.IsEmpty, "the build consumes a version other than the locked release: " + show drift)

    [<Fact>]
    let ``VER-001-O2`` () =
        let lockedIds = lockedComponents () |> List.map (fun c -> c.Id) |> Set.ofList
        let missing = integratedSystems |> List.filter (lockedIds.Contains >> not)
        Assert.True(missing.IsEmpty, "applicable Echelon systems absent from the locked set: " + show missing)
        // Every component the kickoff requires is in the locked set. ROS is Praxis's repository operating system:
        // its installation record must be the locked Praxis release.
        let ros = json ".ros/installation.json"
        Assert.Equal("@echelon-foundry/repository-operating-system", propStr "package" ros)
        Assert.Equal(lockedVersion "praxis", Some(propStr "package_version" ros))
        let required =
            json "kickoff/indy-init.kickoff.json"
            |> prop "components"
            |> Option.map items
            |> Option.defaultValue []
            |> List.filter (fun c -> prop "required" c |> Option.exists (fun r -> r.ValueKind = JsonValueKind.True))
            |> List.map (propStr "id")
        Assert.NotEmpty required
        let uncovered = required |> List.filter (fun id -> id <> "ros" && not (lockedIds.Contains id))
        Assert.True(uncovered.IsEmpty, "kickoff-required components absent from the locked set: " + show uncovered)
        // Every Echelon dependency actually integrated (npm or central NuGet) is a locked component.
        let lockedPackages = lockedComponents () |> List.map (fun c -> c.Package) |> Set.ofList
        let integrated =
            (packageJsonDeps () |> List.map (fun (_, n, _) -> n) |> List.filter isEchelon)
            @ (nugetPins () |> List.map fst |> List.filter (fun n -> n.StartsWith("EchelonFoundry.", StringComparison.Ordinal)))
        Assert.NotEmpty integrated
        let unlocked = integrated |> List.filter (lockedPackages.Contains >> not)
        Assert.True(unlocked.IsEmpty, "integrated Echelon dependencies outside the locked set: " + show unlocked)

    [<Fact>]
    let ``VER-001-O3`` () =
        let locked = lockedComponents ()
        Assert.NotEmpty locked
        let defects =
            locked
            |> List.collect (fun c ->
                let expectedReference =
                    match c.Distribution with
                    | "npm" -> Some(sprintf "npm:%s@%s" c.Package c.Version)
                    | "nuget" -> Some(sprintf "nuget:%s@%s" c.Package c.Version)
                    | "host-tool" -> Some(sprintf "host:%s@%s" c.Id c.Version)
                    | _ -> None
                [ if expectedReference.IsNone then yield c.Id + ": unknown release channel '" + c.Distribution + "'"
                  if String.IsNullOrWhiteSpace c.Package then yield c.Id + ": no owning package"
                  if String.IsNullOrWhiteSpace c.Authority then yield c.Id + ": no selection authority"
                  if not (isSha256 c.DescriptorSha256) then yield c.Id + ": no release descriptor digest"
                  match expectedReference with
                  | Some r when r <> c.SourceReference -> yield sprintf "%s: source reference %s is not %s" c.Id c.SourceReference r
                  | _ -> () ])
        Assert.True(defects.IsEmpty, show defects)
        // Not every system is a runtime dependency: host tools are never application dependencies, and every npm
        // component is a runtime dependency of the application, installed from its own channel.
        let deps = packageJsonDeps ()
        let runtime = deps |> List.filter (fun (s, _, _) -> s = "dependencies") |> List.map (fun (_, n, _) -> n) |> Set.ofList
        let allDeps = deps |> List.map (fun (_, n, _) -> n) |> Set.ofList
        let hostTools = locked |> List.filter (fun c -> c.Distribution = "host-tool")
        Assert.NotEmpty hostTools
        let hostAsDependency = hostTools |> List.filter (fun c -> allDeps.Contains c.Package || allDeps.Contains c.Id) |> List.map (fun c -> c.Id)
        Assert.True(hostAsDependency.IsEmpty, "host tools declared as application dependencies: " + show hostAsDependency)
        let npmNotRuntime = locked |> List.filter (fun c -> c.Distribution = "npm" && not (runtime.Contains c.Package)) |> List.map (fun c -> c.Id)
        Assert.True(npmNotRuntime.IsEmpty, "npm components that are not runtime dependencies: " + show npmNotRuntime)
