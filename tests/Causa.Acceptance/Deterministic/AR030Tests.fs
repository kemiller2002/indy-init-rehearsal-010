// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// AR-030: test-only hooks, state injection, fixture modes and their exports are compiled out of the production build.
namespace Causa.Acceptance.Deterministic

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Xunit
open Causa.TestSupport

module AR030Tests =
    let private production () =
        [ typeof<Causa.Domain.DomainKernel>.Assembly; typeof<Causa.Persistence.SaveMachine>.Assembly; typeof<Causa.App.Model>.Assembly ]

    /// The frozen IF41-VIEW names that label fixture-produced views (a claim carried by a view, never a backend, data
    /// or a switch): Projections.fs declares them for every backend, and no production value constructs them.
    let private frozenLabels = set [ "FixtureLabel"; "FixtureBackend"; "VisibleFixture"; "UnderstoodFixture"; "fixture"; "fixtureLabel"; "create"; "value" ]

    let private hook = Regex("(?i)(fake|stub|mock|testhook|test_hook|inject|fixture|backdoor|debugstate|setstate)")

    let private names (a: Assembly) =
        [ for t in a.GetTypes() do
              yield string t.FullName, t.Name
              for m in t.GetMembers(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly) do
                  yield sprintf "%s.%s" (string t.FullName) m.Name, m.Name ]

    [<Fact>]
    let ``AR-030-O1`` () =
        for a in production () do
            let refs = a.GetReferencedAssemblies() |> Array.map (fun n -> string n.Name)
            for testOnly in [ "Causa.TestSupport"; "xunit.core"; "xunit.assert"; "Microsoft.VisualStudio.TestPlatform.TestFramework" ] do
                Assert.DoesNotContain(testOnly, refs)
            for full, name in names a do
                let simple = name.Split('`')[0]
                if hook.IsMatch simple && not (frozenLabels.Contains simple) && not (full.Contains "FixtureLabel" || full.Contains "BackendOrigin" || full.Contains "Visible" || full.Contains "Understood") then
                    failwithf "%s in %s is a test hook, injection point or fixture mode" full (a.GetName().Name)
        // No production value constructs a fixture-labelled view: the label's constructor is referenced by no production
        // source outside its own declaration (Projections.fs).
        let root = Layers.repositoryRoot ()
        for path, text in PfKit.sources "src" [ ".fs"; ".cs" ] do
            if not (path.EndsWith "Projections.fs") then
                Assert.False(text.Contains "FixtureLabel.create" || text.Contains "FixtureBackend" || text.Contains "VisibleFixture", sprintf "%s constructs a fixture view" path)
        Assert.True(Directory.Exists(Path.Combine(root, "src")))

    [<Fact>]
    let ``AR-030-O2`` () =
        // The runtime's only export is the engine's message dispatch.
        let host = File.ReadAllText(Path.Combine(Layers.repositoryRoot (), "src", "Causa.Host", "Host.cs"))
        Assert.Equal(1, Regex.Matches(host, @"^\s*\[JSExport\]", RegexOptions.Multiline).Count)
        Assert.Contains("public static string Dispatch(string messageJson)", host)
        // The page sets no global through which a hook could be enabled: the boundary's only global assignment is the
        // no-store fetch mechanism, and no page or screen script defines a global or reads a test switch.
        let web = PfKit.sources "web" [ ".js"; ".html" ]
        let dist = PfKit.sources "dist" [ ".js"; ".html" ] |> List.filter (fun (p, _) -> not (p.Contains "_framework") && not (p.Contains "limen"))
        Assert.NotEmpty(web)
        let assignment = Regex(@"(?:window|globalThis|self)\s*\.\s*([A-Za-z_$][\w$]*)\s*=(?!=)")
        let switch = Regex(@"(?i)(__causa|causaTest|testMode|fixtureMode|localStorage\.getItem\(|URLSearchParams[^\n]*(test|fixture|debug))")
        for path, text in web @ dist do
            for m in assignment.Matches text do
                Assert.True((m.Groups[1].Value = "fetch"), sprintf "%s sets the global %s" path m.Groups[1].Value)
            Assert.False(switch.IsMatch text, sprintf "%s reads a test switch: %s" path (switch.Match text).Value)
        // The engine's dispatch accepts no message that injects state: its message kinds are the IF41-APP ones.
        let program = File.ReadAllText(Path.Combine(Layers.repositoryRoot (), "src", "Causa.App", "Program.fs"))
        Assert.False(hook.IsMatch(Regex.Replace(program, @"//[^\n]*", "")), "Program.fs names a hook, an injection or a fixture mode")
