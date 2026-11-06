// Owner: domain. PERSIST-051: the enumerating F# test of the save-state machine and the compile-time negative check.
namespace Causa.Acceptance.Deterministic

open System
open System.Diagnostics
open System.IO
open Xunit
open Causa.Persistence
open Causa.Acceptance.Deterministic.SaveFixture

module PERSIST051Tests =
    let private caseNames (t: Type) =
        Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(t) |> Array.map (fun c -> c.Name) |> List.ofArray

    /// Compiles one F# file against the built Causa.Domain and Causa.Persistence assemblies, as application code in
    /// another assembly would. Returns the exit code and the compiler output.
    let private compile (name: string) (source: string) : int * string =
        let dir = Path.Combine(Path.GetTempPath(), $"causa-persist051-{name}-{Environment.ProcessId}")
        if Directory.Exists dir then Directory.Delete(dir, true)
        Directory.CreateDirectory dir |> ignore
        let reference (asm: Reflection.Assembly) =
            $"<Reference Include=\"{asm.GetName().Name}\"><HintPath>{asm.Location}</HintPath></Reference>"
        File.WriteAllText(
            Path.Combine(dir, "Probe.fsproj"),
            $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><TreatWarningsAsErrors>false</TreatWarningsAsErrors></PropertyGroup>
  <ItemGroup><Compile Include="Probe.fs" /></ItemGroup>
  <ItemGroup>{reference typeof<Causa.Domain.InvestigationId>.Assembly}{reference typeof<SaveMachine>.Assembly}</ItemGroup>
</Project>"""
        )
        File.WriteAllText(Path.Combine(dir, "Probe.fs"), source)
        let dotnet =
            match Environment.GetEnvironmentVariable "DOTNET_HOST_PATH" with
            | null
            | "" -> "dotnet"
            | path -> path
        let info = ProcessStartInfo(dotnet, "build -nologo -clp:NoSummary")
        info.WorkingDirectory <- dir
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        info.Environment["DOTNET_NOLOGO"] <- "1"
        use p = Process.Start info |> nonNull
        let out = p.StandardOutput.ReadToEndAsync()
        let err = p.StandardError.ReadToEnd()
        if not (p.WaitForExit 300000) then
            p.Kill true
            failwith "the probe build did not finish in 300 s"
        p.ExitCode, out.Result + err

    /// Application code (namespace Causa.App, another assembly) forging the confirmation Saved rests on.
    let private forge =
        """namespace Causa.App
module ForgeConfirmation =
    let forged : Causa.Persistence.Confirmation.T =
        { Investigation = Causa.Domain.InvestigationId System.Guid.Empty
          Commit = Causa.Persistence.CommitOid "forged"
          CommittedAt = System.DateTimeOffset.MinValue }
"""

    /// The control: the same assembly obtaining one the only legal way, from an equal read-back.
    let private control =
        """namespace Causa.App
module ObtainConfirmation =
    let bytes : Causa.Persistence.FileSet = { Manifest = [| 1uy |]; Events = [| 2uy |]; Snapshot = [| 3uy |] }
    let confirmed : Causa.Persistence.Confirmation.T option =
        Causa.Persistence.Confirmation.ofReadBack (Causa.Domain.InvestigationId System.Guid.Empty) (Causa.Persistence.CommitOid "c1") System.DateTimeOffset.MinValue bytes bytes
"""

    [<Fact>]
    let ``PERSIST-051-O1`` () =
        // The enumeration covers every save state and every input the machine's unions declare.
        Assert.Equal<string list>(caseNames typeof<SaveState>, states)
        Assert.Equal<string list>(caseNames typeof<SaveInput>, inputs |> List.map inputName)
        let m = machine ()
        let pairs = decideAll m |> List.map (fun (s, i, _, _) -> s, inputName i) |> set
        Assert.Equal(7 * 27, pairs.Count)
        Assert.Equal(50, legal.Count)

    [<Fact>]
    let ``PERSIST-051-O2`` () =
        // Each legal transition reaches the state PERSIST-031 defines, each illegal input fails closed with a typed
        // fault, and Saved is reached only from a read-back confirmation.
        Assert.Empty(pairViolations (machine ()))

    [<Fact>]
    let ``PERSIST-051-O3`` () =
        // Compile-time negative check: application code outside the persistence module cannot construct the
        // confirmation (FS1093: the union cases or fields of Confirmation.T are not accessible) ...
        let forgeExit, forgeOutput = compile "forge" forge
        Assert.NotEqual(0, forgeExit)
        Assert.Contains("FS1093", forgeOutput)
        // ... and the failure is for that reason alone: the same probe obtaining it through ofReadBack compiles.
        let controlExit, controlOutput = compile "control" control
        Assert.True((controlExit = 0), controlOutput)

    [<Fact>]
    let ``PERSIST-051-O4`` () =
        // The full PAT-12 run on this build: all 189 pairs decided as the contract says.
        let m = machine ()
        let all = decideAll m
        Assert.Equal(189, all.Length)
        Assert.Equal(50, all |> List.filter (fun (_, _, _, r) -> Result.isOk r) |> List.length)
        Assert.Empty(pairViolations m)

    [<Fact>]
    let ``PERSIST-051-O5`` () =
        let m = machine ()
        // PERSIST-031: exactly the seven save states.
        Assert.Equal<string list>([ "Saved"; "Unsaved"; "Saving"; "Offline"; "Conflict"; "UnknownOutcome"; "SaveFailed" ], states)
        // PERSIST-007: a save is Saved only after the read-back at its commit matched, and its confirmation names it.
        let saving = sessionIn m "Saving" (SaveInput.ReadBackMatched(confirmation "c1"))
        match m.Step (context 3L) saving (SaveInput.ReadBackMatched(confirmation "c1")) with
        | Ok(next, _) ->
            match next.State with
            | SaveState.Saved(Confirmed c) ->
                Assert.Equal(CommitOid "c1", c.CommitOid)
                Assert.Equal(CommitOid "c1", next.BaseHead)
            | other -> failwithf "a matched read-back with no change during the save reached %A" other
        | Error f -> failwithf "a matched read-back was refused: %A" f
        // PERSIST-023: the confirmation cannot be built from unequal bytes.
        Assert.True((Confirmation.ofReadBack investigationId (CommitOid "c1") now (bytes 1uy) (bytes 2uy)).IsNone)
