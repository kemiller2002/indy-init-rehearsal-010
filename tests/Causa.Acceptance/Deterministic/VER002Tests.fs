// Owner: integration. T10 release baseline (VER-002): the locked set comes from authoritative release sources, is
// recorded with its source and selection basis, and is never a branch head.
namespace Causa.Acceptance.Deterministic

open System
open System.Globalization
open System.Text.RegularExpressions
open Xunit
open ReleaseBaseline

module VER002Tests =
    /// The release asset or registry tarball an Echelon package resolves from.
    let authoritativeSource (p: LockedPackage) =
        let registry = sprintf "https://registry.npmjs.org/%s/-/" p.Name
        let release = Regex(@"^https://github\.com/kemiller2002/[^/]+/releases/download/v" + Regex.Escape p.Version + "/[^/]+\.tgz$")
        p.Resolved.StartsWith(registry, StringComparison.Ordinal) || release.IsMatch p.Resolved

    [<Fact>]
    let ``VER-002-O1`` () =
        let echelon = installedPackages () |> List.filter (fun p -> isEchelon p.Name)
        Assert.NotEmpty echelon
        let unsourced = echelon |> List.filter (authoritativeSource >> not) |> List.map (fun p -> p.Name + " <- " + p.Resolved)
        Assert.True(unsourced.IsEmpty, "Echelon packages not resolved from a registry or a published release: " + show unsourced)
        let unhashed = echelon |> List.filter (fun p -> not (p.Integrity.StartsWith("sha512-", StringComparison.Ordinal))) |> List.map (fun p -> p.Name)
        Assert.True(unhashed.IsEmpty, "Echelon packages without a registry integrity digest: " + show unhashed)
        // The requirement sources the lock records are each pinned to an exact commit of their owning repository.
        let requirements = lock () |> prop "requirements" |> Option.map items |> Option.defaultValue []
        Assert.NotEmpty requirements
        let reference = Regex(@"^github:[^/#]+/[^/#]+#([0-9a-f]{40})\|file:.+$")
        let unpinned =
            requirements
            |> List.filter (fun r ->
                let m = reference.Match(propStr "sourceReference" r)
                not m.Success || not (System.IO.File.Exists(at (propStr "targetPath" r))))
            |> List.map (propStr "id")
        Assert.True(unpinned.IsEmpty, "lock requirements without an exact source commit or a materialized target: " + show unpinned)

    [<Fact>]
    let ``VER-002-O2`` () =
        // Candidate release, source reference and selection basis per locked component.
        let incomplete =
            lockedComponents ()
            |> List.filter (fun c ->
                String.IsNullOrWhiteSpace c.Version || String.IsNullOrWhiteSpace c.SourceReference
                || String.IsNullOrWhiteSpace c.Authority || not (isSha256 c.DescriptorSha256))
            |> List.map (fun c -> c.Id)
        Assert.True(incomplete.IsEmpty, "locked components without candidate, source, basis and digest: " + show incomplete)
        Assert.True(isSha256 (propStr "manifestSha256" (lock ())), "the lock does not record its manifest digest")
        // The owner-decided toolchain lock that records why this set was selected is named and pinned by commit.
        let governance = json ".echelon/r9-governance.json"
        Assert.Matches(@"^kickoff/rehearsal-\d{3}/toolchain\.lock\.json$", propStr "toolchainLock" governance)
        Assert.True(isCommit (propStr "indyInitCommit" governance), "the governance record does not pin the Indy-init commit")
        // Checked timestamp: the baseline was locked at T0 and implementation authorized after it.
        let utc (s: string) = DateTimeOffset.ParseExact(s, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
        let t0 = utc (propStr "t0" (json "evidence/t0/t0.json"))
        let authorized = utc (propStr "authorizedAt" (json ".echelon/r9-initialization-checkpoint.json"))
        Assert.True(authorized >= t0, "implementation was authorized before the baseline was locked")
        Assert.Equal(t0.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), propStr "installed_on" (json ".ros/installation.json"))

    [<Fact>]
    let ``VER-002-O3`` () =
        // Every dependency is an exact released version or a published release asset, never a git ref or branch.
        let deps = packageJsonDeps ()
        Assert.NotEmpty deps
        let branchLike (spec: string) =
            Regex.IsMatch(spec, @"^(github:|git\+|git:|[^/@\s]+/[^/@\s]+(#.*)?$)|#|/archive/|/tree/|/tarball/")
        let notReleased = deps |> List.filter (fun (_, _, spec) -> branchLike spec || (specVersion spec).IsNone) |> List.map (fun (_, n, s) -> n + "@" + s)
        Assert.True(notReleased.IsEmpty, "dependencies not pinned to a released artifact: " + show notReleased)
        let gitResolved = installedPackages () |> List.filter (fun p -> p.Resolved.StartsWith("git", StringComparison.Ordinal)) |> List.map (fun p -> p.Name)
        Assert.True(gitResolved.IsEmpty, "packages installed from git: " + show gitResolved)
        // Echelon reusable workflows are pinned to an exact commit, not a moving branch.
        let echelonUses =
            workflows ()
            |> List.collect (fun (file, body) ->
                [ for m in Regex.Matches(body, @"uses:\s*(kemiller2002/[^@\s]+)@(\S+)") -> file, m.Groups.[1].Value, m.Groups.[2].Value ])
        Assert.NotEmpty echelonUses
        let floating = echelonUses |> List.filter (fun (_, _, r) -> not (isCommit r)) |> List.map (fun (f, u, r) -> sprintf "%s: %s@%s" f u r)
        Assert.True(floating.IsEmpty, "Echelon workflows not pinned to a commit: " + show floating)
