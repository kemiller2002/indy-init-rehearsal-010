// Owner: integration. T10 release baseline (VER-005): no pre-release, unpublished or local build is adopted, and every
// adopted Echelon artifact carries provenance (a registry integrity digest and a release descriptor digest).
namespace Causa.Acceptance.Deterministic

open System
open Xunit
open ReleaseBaseline

module VER005Tests =
    /// Every version pin in the repository: (where, name, version).
    let allPins () =
        [ yield! lockedComponents () |> List.map (fun c -> ".conditor/lock.json", c.Id, c.Version)
          yield! echelonPins () |> List.map (fun (w, id, v, _) -> w, id, v)
          yield! nugetPins () |> List.map (fun (n, v) -> "Directory.Packages.props", n, v)
          yield! packageJsonDeps () |> List.map (fun (_, n, s) -> "package.json", n, (specVersion s |> Option.defaultValue s))
          yield! directInstalled () |> List.map (fun p -> "package-lock.json", p.Name, p.Version)
          yield "global.json", "sdk", (json "global.json" |> prop "sdk" |> Option.map (propStr "version") |> Option.defaultValue "") ]

    [<Fact>]
    let ``VER-005-O1`` () =
        let pins = allPins ()
        Assert.True(pins.Length >= 20, sprintf "only %d pins found" pins.Length)
        let prerelease = pins |> List.filter (fun (_, _, v) -> not (isStable v)) |> List.map (fun (w, n, v) -> sprintf "%s: %s %s" w n v)
        Assert.True(prerelease.IsEmpty, "pre-release, unpublished or non-exact versions: " + show prerelease)
        let local =
            packageJsonDeps ()
            |> List.filter (fun (_, _, s) -> [ "file:"; "link:"; "workspace:"; "portal:"; "./"; "../"; "/" ] |> List.exists (fun p -> s.StartsWith(p, StringComparison.Ordinal)))
            |> List.map (fun (_, n, s) -> n + "@" + s)
        Assert.True(local.IsEmpty, "local builds adopted as dependencies: " + show local)
        let linked = installedPackages () |> List.filter (fun p -> p.IsLink || p.Resolved.StartsWith("file:", StringComparison.Ordinal)) |> List.map (fun p -> p.Name)
        Assert.True(linked.IsEmpty, "installed packages linked from a local path: " + show linked)

    [<Fact>]
    let ``VER-005-O2`` () =
        // Provenance: each adopted Echelon npm artifact has a sha512 integrity digest of a 64-byte hash.
        let echelon = directInstalled () |> List.filter (fun p -> isEchelon p.Name)
        Assert.NotEmpty echelon
        let digestOk (integrity: string) =
            integrity.StartsWith("sha512-", StringComparison.Ordinal)
            && (try Convert.FromBase64String(integrity.Substring 7).Length = 64 with _ -> false)
        let unproven = echelon |> List.filter (fun p -> not (digestOk p.Integrity)) |> List.map (fun p -> p.Name)
        Assert.True(unproven.IsEmpty, "Echelon artifacts without a verifiable integrity digest: " + show unproven)
        // Each locked component carries its release descriptor digest and source.
        let undescribed = lockedComponents () |> List.filter (fun c -> not (isSha256 c.DescriptorSha256) || c.SourceReference = "") |> List.map (fun c -> c.Id)
        Assert.True(undescribed.IsEmpty, "locked components without a release descriptor digest: " + show undescribed)
        // No non-stable pin exists, so none needs an approval; any that appeared would have to be approved. The
        // repository records no such approval, so the set of non-stable pins must be empty.
        let nonStable = allPins () |> List.filter (fun (_, _, v) -> not (isStable v))
        Assert.Empty nonStable
