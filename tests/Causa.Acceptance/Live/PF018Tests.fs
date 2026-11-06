// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-018 (live): traceability of fixtures and tests to requirements and source commits; obligations preserved.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PF018Tests =
    let private requirementOf (obligation: string) = obligation.Substring(0, obligation.LastIndexOf "-O")

    let private planObligations (plan: Text.Json.JsonDocument) =
        [ for o in plan.RootElement.GetProperty("obligations").EnumerateArray() ->
              string (o.GetProperty("id").GetString()), string (o.GetProperty("requirement").GetString()), string (o.GetProperty("priority").GetString()), string (o.GetProperty("criterion").GetString()) ]

    /// O1: each fixture names the requirements it serves and its authoritative source commit; each acceptance test is
    /// mapped by the plan to the obligations of its own requirement; and a fixture written to GitHub records its source.
    let o1 (mk: string -> GitHubProvider) =
        use plan = Layers.plan ()
        let requirements = planObligations plan |> List.map (fun (_, r, _, _) -> r) |> set
        for name, reqs, source in FixtureData.traces do
            Assert.NotEmpty(reqs)
            Assert.All(reqs, fun r -> Assert.True(requirements.Contains r, sprintf "%s names %s, which the plan does not have" name r))
            Assert.False(String.IsNullOrWhiteSpace source)
        Assert.Matches("^[0-9a-f]{40}$", FixtureData.sourceCommit)
        for t in Layers.tests plan do
            let m = Text.RegularExpressions.Regex.Match(t.Id, "^dotnet:Causa\\.Acceptance\\.(Live|Deterministic)\\.([A-Z]+)(\\d{3})Tests\\.")
            if m.Success then
                Assert.NotEmpty(t.Obligations)
                let expected = m.Groups[2].Value.Replace("EXTDATA", "EXT-DATA") + "-" + m.Groups[3].Value
                Assert.All(t.Obligations, fun o -> Assert.Equal(expected, requirementOf o))
        let real = mk "pf018-o1"
        let commit = FixtureData.seed real (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog"
        let message = Scenarios.commitMessage real commit |> Probes.ok "message"
        Assert.Contains(sprintf "Source: %s@%s" FixtureData.sourceRepository FixtureData.sourceCommit, message)

    /// The P0 obligations of PROVIDER-FIXTURE-AND-INTEGRATION-REQUIREMENTS.md, per requirement (the source of truth
    /// the rehearsal package was derived from).
    let private sourceObligations =
        [ "PF-001", 3; "PF-002", 2; "PF-003", 4; "PF-004", 3; "PF-005", 2; "PF-006", 2; "PF-007", 2; "PF-008", 2; "PF-009", 3
          "PF-011", 6; "PF-012", 3; "PF-013", 4; "PF-014", 4; "PF-015", 5; "PF-016", 2; "PF-017", 2; "PF-018", 2; "PF-019", 2; "PF-020", 2 ]

    /// O2: derivation preserved every P0 obligation: each is in the plan, still P0, with its criterion, and has at
    /// least one test; a missing or weakened one fails here.
    let o2 (mk: string -> GitHubProvider) =
        let _ = mk "pf018-o2"
        use plan = Layers.plan ()
        let obligations = planObligations plan
        let tests = Layers.tests plan
        for requirement, count in sourceObligations do
            for n in 1..count do
                let id = sprintf "%s-O%d" requirement n
                match obligations |> List.tryFind (fun (o, _, _, _) -> o = id) with
                | None -> failwithf "%s is missing from the plan" id
                | Some(_, r, priority, criterion) ->
                    Assert.Equal(requirement, r)
                    Assert.True((priority = "P0"), sprintf "%s was weakened to %s" id priority)
                    Assert.False(String.IsNullOrWhiteSpace criterion)
                    Assert.True(tests |> List.exists (fun t -> List.contains id t.Obligations), sprintf "%s has no test" id)

    [<Fact>]
    let ``PF-018-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-018-O2`` () = o2 PfKit.live
