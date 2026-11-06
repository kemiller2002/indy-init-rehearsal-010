// Owner: integration. WI-GEN-VER (VER-010): the VER requirements are reconciled with implementation and tests in this
// rehearsal, and work items enter through the repository's Praxis input path, not GitHub Issues.
namespace Causa.Acceptance.Deterministic

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Xunit
open ReleaseBaseline

module VER010Tests =
    /// The VER requirements VER-010-O1 names: VER-001 .. VER-010.
    let verRequirements = [ for n in 1..10 -> sprintf "VER-%03d" n ]

    let obligations = json ".echelon/obligations.json"

    /// Frozen-plan obligation ids per requirement.
    let planObligations (requirement: string) =
        obligations |> prop "obligations" |> Option.map items |> Option.defaultValue []
        |> List.filter (fun o -> propStr "requirement" o = requirement)
        |> List.map (propStr "id")

    /// Test identities bound to an obligation.
    let identitiesOf (obligation: string) =
        obligations |> prop "tests" |> Option.map items |> Option.defaultValue []
        |> List.filter (fun t -> prop "obligations" t |> Option.map items |> Option.defaultValue [] |> List.exists (fun o -> str o = obligation))
        |> List.map (propStr "id")

    /// Whether a `dotnet:<Namespace>.<Module>.<Fact>` identity is a runnable xunit Fact in this test assembly.
    let isRunnableFact (identity: string) =
        let name = identity.Substring "dotnet:".Length
        let cut = name.LastIndexOf '.'
        let typeName, methodName = name.Substring(0, cut), name.Substring(cut + 1)
        match Assembly.GetExecutingAssembly().GetType typeName with
        | null -> false
        | t ->
            match t.GetMethod(methodName, BindingFlags.Public ||| BindingFlags.Static) with
            | null -> false
            | m ->
                match m.GetCustomAttribute<FactAttribute>() with
                | null -> false
                | fact -> String.IsNullOrEmpty fact.Skip

    [<Fact>]
    let ``VER-010-O1`` () =
        // Requirements outside the frozen plan are the approved scope exclusions: the plan is the owner-approved scope
        // (P0 requirements of the v4 plan) pinned by the scope checkpoint, which must have passed preflight and coverage.
        let scope = json ".echelon/scope-checkpoint.json"
        Assert.True(isSha256 (propStr "scopeSha256" scope), "the scope checkpoint pins no scope digest")
        Assert.Equal("PASS", propStr "preflight" scope)
        Assert.Equal("PASS", propStr "coverage" scope)
        let inPlan = verRequirements |> List.filter (fun r -> not (planObligations r).IsEmpty)
        let excluded = verRequirements |> List.filter (fun r -> (planObligations r).IsEmpty)
        // Each exclusion is a requirement VER-010 knowingly depends on, not a silent omission.
        let dependsOn =
            planObligations "VER-010"
            |> List.collect (fun id ->
                obligations |> prop "obligations" |> Option.map items |> Option.defaultValue []
                |> List.filter (fun o -> propStr "id" o = id)
                |> List.collect (fun o -> prop "dependencies" o |> Option.map items |> Option.defaultValue [] |> List.map str))
            |> Set.ofList
        Assert.Contains("VER-010", inPlan)
        let unexplained = excluded |> List.filter (dependsOn.Contains >> not)
        Assert.True(unexplained.IsEmpty, "VER requirements neither planned nor named as excluded dependencies: " + show unexplained)
        // Every planned VER obligation is bound to a test, and every executable identity is a runnable Fact here.
        let gaps =
            inPlan
            |> List.collect planObligations
            |> List.collect (fun o ->
                match identitiesOf o with
                | [] -> [ o + ": no test identity" ]
                | ids ->
                    ids
                    |> List.filter (fun id -> id.StartsWith("dotnet:Causa.Acceptance.", StringComparison.Ordinal) && not (isRunnableFact id))
                    |> List.map (fun id -> o + ": " + id + " is not a runnable Fact"))
        Assert.True(gaps.IsEmpty, "VER obligations not reconciled with tests: " + show gaps)

    [<Fact>]
    let ``VER-010-O2`` () =
        // Every planned work item enters through the Praxis queue (.ros/work/queue.json), once.
        let queue = json ".ros/work/queue.json" |> prop "items" |> Option.map items |> Option.defaultValue []
        let planned = json ".echelon/r9-work-plan.json" |> prop "workItems" |> Option.map items |> Option.defaultValue [] |> List.map (propStr "id")
        Assert.NotEmpty planned
        let queued (id: string) = queue |> List.filter (fun q -> (propStr "title" q).StartsWith("[" + id + "]", StringComparison.Ordinal))
        let notQueued = planned |> List.filter (fun id -> (queued id).Length <> 1)
        Assert.True(notQueued.IsEmpty, "planned work items not entered exactly once in the Praxis queue: " + show notQueued)
        // No queue item came from GitHub Issues.
        let fromIssues =
            queue
            |> List.filter (fun q ->
                let s = (propStr "source" q + " " + propStr "sourceReference" q).ToLowerInvariant()
                s.Contains "issue" || Regex.IsMatch(s, @"github\.com/[^/]+/[^/]+/issues/"))
            |> List.map (propStr "id")
        Assert.True(fromIssues.IsEmpty, "queue items sourced from GitHub Issues: " + show fromIssues)
        // Every work event in the Praxis log refers to a queued work item.
        let ids = queue |> List.map (propStr "id") |> Set.ofList
        let events = File.ReadAllLines(at ".ros/events/events.jsonl") |> Array.filter (String.IsNullOrWhiteSpace >> not) |> Array.map (fun l -> System.Text.Json.JsonDocument.Parse(l).RootElement)
        let started = events |> Array.filter (fun e -> propStr "type" e = "work.started")
        Assert.NotEmpty started
        let stray =
            events
            |> Array.filter (fun e -> (propStr "type" e).StartsWith("work.", StringComparison.Ordinal))
            |> Array.map (propStr "workItem")
            |> Array.filter (fun w -> not (ids.Contains w) && not (w.StartsWith("ROS-INSTALL-", StringComparison.Ordinal)))
            |> Array.distinct
            |> List.ofArray
        Assert.True(stray.IsEmpty, "work events for items outside the Praxis queue: " + show stray)
        // The repository offers no GitHub Issues intake and no automation that files work as issues.
        Assert.False(Directory.Exists(at ".github/ISSUE_TEMPLATE"), "the repository has a GitHub issue intake")
        let issueAutomation =
            workflows ()
            |> List.filter (fun (_, body) -> Regex.IsMatch(body, @"issues:\s*write|gh issue create|/issues\b|create-issue|issue-ops"))
            |> List.map (fun (f, _) -> match f with | null -> "" | s -> s)
        Assert.True(issueAutomation.IsEmpty, "workflows that file work as GitHub Issues: " + show issueAutomation)
