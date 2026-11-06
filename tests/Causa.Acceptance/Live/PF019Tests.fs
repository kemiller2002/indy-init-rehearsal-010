// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-019 (live): local-first workflow: the smallest deterministic checks first, integration in batches, the complete
// live suite before release, and no required test ever skipped.
namespace Causa.Acceptance.Live

open System
open System.Reflection
open Xunit
open Causa.TestSupport

module PF019Tests =
    /// O1: the plan orders the work: the smallest deterministic suite depends on nothing, every integration suite runs
    /// after the deterministic suites, development runs the deterministic set, and release runs every suite.
    let o1 (mk: string -> GitHubProvider) =
        let _ = mk "pf019-o1"
        use plan = Layers.plan ()
        let suites = Layers.suites plan
        let suite id = suites |> List.find (fun s -> s.Id = id)
        Assert.Empty((suite "SU-OWN-DOTNET").DependsOn)
        for live in [ "SU-DOTNET-LIVE"; "SU-BROWSER-LIVE" ] do
            let before = Layers.ancestors suites live
            Assert.Contains("SU-OWN-DOTNET", before)
            Assert.Contains("SU-DOTNET-DETERMINISTIC", before)
        let gate = IO.File.ReadAllText(IO.Path.Combine(Layers.repositoryRoot (), ".github", "workflows", "verification-gate.yml"))
        Assert.Contains("if [ \"$MODE\" = \"development\" ]; then suites=\"--suites SU-OWN-DOTNET,SU-DOTNET-DETERMINISTIC", gate)
        Assert.Contains("suites=\"--require-tests true\"", gate)

    /// O2: no test is skipped: no test source skips or disables a test, and every acceptance test the plan requires
    /// exists in this assembly as a Fact.
    let o2 (mk: string -> GitHubProvider) =
        let _ = mk "pf019-o2"
        let skip = Text.RegularExpressions.Regex("Skip\\s*=|\\.skip\\(|\\.fixme\\(|test\\.only\\(|\\bxit\\(")
        for path, text in PfKit.sources "tests" [ ".fs"; ".js"; ".mjs"; ".ts" ] do
            Assert.False(skip.IsMatch text, sprintf "%s skips a test" path)
        use plan = Layers.plan ()
        let facts =
            Assembly.GetExecutingAssembly().GetTypes()
            |> Seq.collect (fun t -> t.GetMethods(BindingFlags.Public ||| BindingFlags.Static) |> Seq.filter (fun m -> not (isNull (m.GetCustomAttribute<FactAttribute>()))) |> Seq.map (fun m -> sprintf "dotnet:%s.%s" (string t.FullName) m.Name))
            |> set
        let required = Layers.tests plan |> List.filter (fun t -> t.Id.StartsWith "dotnet:Causa.Acceptance.")
        Assert.NotEmpty(required)
        for t in required do
            Assert.True(facts.Contains t.Id, sprintf "%s is required by the plan and is not a Fact of the acceptance assembly" t.Id)

    [<Fact>]
    let ``PF-019-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-019-O2`` () = o2 PfKit.live
