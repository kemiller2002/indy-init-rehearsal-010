// Owner: integration. T11 mandatory gate (VER-008): the build's stack-lock gate verifies the locked manifests against
// what is installed and the compatibility evidence, and anything missing, stale or unverified is BLOCKED or FAIL.
namespace Causa.Acceptance.Deterministic

open System
open System.IO
open Xunit

module VER008Tests =
    open StackLockGate

    let private allChecks =
        [ "sdk-pinned"; "sdk-installed"; "nuget-pinned"; "npm-pinned"; "npm-lock-matches-manifest"; "npm-integrity"
          "npm-installed"; "foundations-match-packages"; "toolchain-match-installed"; "lock-bound-to-rehearsal"
          "evidence-current"; "boundary-compatible" ]

    /// VER-008-O1: the gate runs every check against the installed and loaded artifacts, and the repository passes it.
    [<Fact>]
    let ``VER-008-O1`` () =
        let real = run repoRoot
        Assert.Equal<string list>(List.sort allChecks, real.Checks |> Map.toList |> List.map fst |> List.sort)
        for id in allChecks do
            Assert.True((status real id = "PASS"), sprintf "%s: %s" id (detail real id))
        Assert.Equal("PASS", real.Verdict)
        Assert.Equal(0, real.ExitCode)
        // The loaded artifact is the locked one: the FSharp.Core this test runs on is the centrally pinned version.
        let pinned = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(at "Directory.Packages.props"), "Include=\"FSharp.Core\"\\s+Version=\"([^\"]+)\"").Groups[1].Value
        let loaded =
            typeof<list<int>>.Assembly.GetCustomAttributes(typeof<System.Reflection.AssemblyInformationalVersionAttribute>, false)
            |> Array.map (fun a -> (a :?> System.Reflection.AssemblyInformationalVersionAttribute).InformationalVersion)
            |> Array.exactlyOne
        Assert.StartsWith(pinned, loaded)
        // It is mandatory: `npm run build` is `dotnet fsi build.fsx`, which runs the gate before anything is built.
        Assert.Equal("dotnet fsi build.fsx", (json "package.json").GetProperty("scripts").GetProperty("build").GetString())
        let script = File.ReadAllText(at "build.fsx")
        Assert.True(script.IndexOf("let checks = gate ()") < script.IndexOf("dotnet\" \"publish"), "the gate runs before the publish")

    /// VER-008-O2: missing dependencies are BLOCKED, stale evidence and unverified artifacts FAIL, and a build whose gate
    /// does not pass produces nothing (the gate cannot be skipped into a PASS).
    [<Fact>]
    let ``VER-008-O2`` () =
        // A copy as is passes: what follows fails because of its one change, not because of the copy.
        let control = judgeMutated ignore
        Assert.Equal("PASS", control.Verdict)
        // Missing dependencies: BLOCKED, exit 2.
        let missing = judgeMutated (fun d -> Directory.Delete(Path.Combine(d, "node_modules"), true))
        Assert.Equal("BLOCKED", status missing "npm-installed")
        Assert.Equal("BLOCKED", missing.Verdict)
        Assert.Equal(2, missing.ExitCode)
        // A missing record: BLOCKED, never PASS.
        let noRecord = judgeMutated (fun d -> File.Delete(Path.Combine(d, ".echelon/toolchain.json")))
        Assert.Equal("BLOCKED", status noRecord "toolchain-match-installed")
        Assert.NotEqual<string>("PASS", noRecord.Verdict)
        // Stale evidence: an input changed after the initialization checkpoint recorded its digest.
        let stale = judgeMutated (fun d -> editText d ".echelon/obligations.json" (fun s -> s + " "))
        Assert.Equal("FAIL", status stale "evidence-current")
        Assert.Contains("stale evidence", detail stale "evidence-current")
        Assert.Equal(1, stale.ExitCode)
        // An unverified artifact: a locked package without its digest.
        let unverified = judgeMutated (fun d -> editJson d "package-lock.json" (set [ "packages"; "node_modules/@echelon-foundry/limen"; "integrity" ] "md5-0"))
        Assert.Equal("FAIL", status unverified "npm-integrity")
        // A build over a failing gate writes nothing.
        let dir = copy ()
        let out = Path.Combine(dir, "site")
        try
            editText dir ".echelon/obligations.json" (fun s -> s + " ")
            let code, output = runDotnet repoRoot (sprintf "fsi \"%s\" --root \"%s\" --out \"%s\"" (at "build.fsx") dir out) 120000
            Assert.Equal(1, code)
            Assert.Contains("nothing was built", output)
            Assert.False(Directory.Exists out)
        finally
            remove dir
