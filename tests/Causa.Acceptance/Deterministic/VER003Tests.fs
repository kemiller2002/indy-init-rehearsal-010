// Owner: integration. T10 release baseline (VER-003): the locked set is adopted only through CI that builds and runs
// the acceptance suites against it, a pin mismatch fails the gate, and nothing silently falls back to another version.
namespace Causa.Acceptance.Deterministic

open System
open System.Text.RegularExpressions
open Xunit
open ReleaseBaseline

module VER003Tests =
    let workflow (name: string) =
        workflows () |> List.tryFind (fun (f, _) -> f = name) |> Option.map snd |> Option.defaultWith (fun () -> failwithf "%s is missing" name)

    [<Fact>]
    let ``VER-003-O1`` () =
        // Build and tests run on every pull request, and an empty or skipped run is not a pass.
        let build = workflow "build-and-test.yml"
        Assert.Contains("pull_request", build)
        Assert.Matches(@"dotnet build \S+ -c Release", build)
        Assert.Matches(@"dotnet test \S+ -c Release", build)
        Assert.Contains("An empty test run is not a pass", build)
        // The verification gate is the governed one, installs the locked toolchain and judges the frozen plan.
        let governance = json ".echelon/r9-governance.json"
        Assert.Equal(propStr "workflowSha256" governance, sha256File ".github/workflows/verification-gate.yml")
        let gate = workflow "verification-gate.yml"
        Assert.Contains("pull_request", gate)
        Assert.Contains("install-conditor --lock", gate)
        Assert.Contains("dokimos verification compile", gate)

    [<Fact>]
    let ``VER-003-O2`` () =
        // Every pin the gate verifies fails the gate on mismatch rather than continuing with another release.
        let gate = workflow "verification-gate.yml"
        let checks = Regex.Matches(gate, @"sha256sum -c").Count
        let failClosed = Regex.Matches(gate, @"sha256sum -c -( \S+)? \|\| \{ echo ""::error::[^""]*""; exit 1; \}").Count
        Assert.True(checks >= 2, sprintf "the gate verifies only %d pins" checks)
        Assert.Equal(checks, failClosed)
        // The only work completed before governance bound the lock is the toolchain installation itself; any other
        // exception to the locked set would have to be documented here.
        let before = json ".echelon/r9-governance.json" |> prop "completedBeforeGovernance" |> Option.map items |> Option.defaultValue [] |> List.map str
        Assert.Equal<string list>([ "ROS-INSTALL-" + (lockedVersion "praxis" |> Option.defaultValue "?").Replace('.', '-') ], before)

    [<Fact>]
    let ``VER-003-O3`` () =
        // The SDK is exact and never rolls forward or back.
        let sdk = json "global.json" |> prop "sdk" |> Option.defaultWith (fun () -> failwith "global.json has no sdk")
        Assert.True(isStable (propStr "version" sdk), "the SDK is not an exact release")
        Assert.Equal("disable", propStr "rollForward" sdk)
        // No floating or ranged versions anywhere a package is pinned.
        let nuget = nugetPins ()
        Assert.NotEmpty nuget
        let floatingNuget = nuget |> List.filter (fun (_, v) -> not (isStable v)) |> List.map (fun (n, v) -> n + " " + v)
        Assert.True(floatingNuget.IsEmpty, "central NuGet pins that are not exact: " + show floatingNuget)
        let ranged = packageJsonDeps () |> List.filter (fun (_, _, s) -> Regex.IsMatch(s, @"^[\^~<>=*]|\sx$|\.x|\|\||\s-\s|^latest$")) |> List.map (fun (_, n, s) -> n + "@" + s)
        Assert.True(ranged.IsEmpty, "npm dependencies with ranges: " + show ranged)
        // The installed tree is the committed lockfile: each direct dependency is installed at its pinned version.
        let pinned = packageJsonDeps () |> List.choose (fun (_, n, s) -> specVersion s |> Option.map (fun v -> n, v)) |> Map.ofList
        let installed = directInstalled ()
        Assert.Equal(pinned.Count, installed.Length)
        let drift = installed |> List.filter (fun p -> pinned.[p.Name] <> p.Version) |> List.map (fun p -> sprintf "%s %s (pinned %s)" p.Name p.Version pinned.[p.Name])
        Assert.True(drift.IsEmpty, "installed versions differ from the pins: " + show drift)
