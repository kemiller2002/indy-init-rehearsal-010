// Owner: ui-2. The ui-2 screens as consumers of IF41-DOMAIN and IF41-VIEW: investigation-start (INV-001..006,
// INV-019..023), the workspace frame and Details render what the kernel projects from real engine states.
module Causa.Contract.Tests.Domain.ConsumerUi2

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui
open Causa.App.Views
open Causa.TestSupport.UiFixtures

let private text (key: string) (view: (string * ViewValue) list) =
    match view |> List.tryFind (fst >> (=) key) with
    | Some(_, BindingValue.Text s) -> s
    | other -> failwithf "key %s is not text: %A" key other

let private flag (key: string) (view: (string * ViewValue) list) =
    match view |> List.tryFind (fst >> (=) key) with
    | Some(_, BindingValue.Flag b) -> b
    | other -> failwithf "key %s is not a flag: %A" key other

let private messages (key: string) (view: (string * ViewValue) list) =
    match view |> List.tryFind (fst >> (=) key) with
    | Some(_, BindingValue.Items rows) ->
        rows |> List.map (fun r -> match r.["message"] with BindingPrimitive.Text s -> s | p -> failwithf "%A" p)
    | other -> failwithf "key %s is not a list: %A" key other

let private keysOf (view: (string * ViewValue) list) = view |> List.map fst |> Set.ofList

let private guid = "3f2504e0-4f89-41d3-9a0c-0305e82c3301"

[<Fact>]
let KernelSignaturesAndFixtures () =
    let s = Ui2Scenarios.stateAt (Ui2Scenarios.connectedModel true "#/new")

    // ---- investigation-start: every key always present, whatever the state (Limen throws on a missing key) --------
    let fresh = Start.view (s [ Start.IdentifierEvent, guid ])
    let refusedEmpty = Start.view (s [ Start.IdentifierEvent, guid; Start.AttemptedKey, "1" ])
    Assert.Equal<Set<string>>(keysOf fresh, keysOf refusedEmpty)
    Assert.Equal<Set<string>>(keysOf fresh, keysOf (Start.view (Ui2Scenarios.stateAt (Update.initialModel (Routes.route "#/")) [])))
    Assert.True(keysOf fresh |> Set.forall (fun k -> k.StartsWith "new."), "every Start key is prefixed by its screen name")

    // INV-006: the identifier the engine generated is the field's value; INV-001/002/023 values are the drafts.
    Assert.Equal(guid, text "new.identifier" fresh)
    let entered =
        Start.view (s [ Start.ProblemEvent, "Pump P-101 tripped"; Start.TitleEvent, "Pump trip"; Start.QuestionEvent, "Why did it trip?"; Start.IdentifierEvent, guid ])
    Assert.Equal("Pump P-101 tripped", text "new.problem" entered)
    Assert.Equal("Pump trip", text "new.title" entered)
    Assert.Equal("Why did it trip?", text "new.question" entered)
    Assert.False(flag "new.refused" entered)
    Assert.Empty(messages "new.summary" entered)

    // Nothing is shown as an error before the user edits a field or tries to start.
    Assert.Empty(messages "new.problem.errors" fresh)
    // INV-019: an empty or whitespace-only problem statement is refused with a field message.
    Assert.Equal<string list>([ "Problem statement must not be empty." ], messages "new.problem.errors" refusedEmpty)
    Assert.True(flag "new.refused" refusedEmpty)
    Assert.Equal("true", text "new.problem.invalid" refusedEmpty)
    let blank = Start.view (s [ Start.ProblemEvent, "   \n "; Start.IdentifierEvent, guid ])
    Assert.Equal<string list>([ "Problem statement must not be empty." ], messages "new.problem.errors" blank)
    // INV-021, INV-020, INV-023: maximum lengths, at the boundary and one over.
    let at10000 = Start.view (s [ Start.ProblemEvent, String('p', 10000); Start.QuestionEvent, String('q', 10000); Start.TitleEvent, String('t', 200) ])
    Assert.Empty(messages "new.problem.errors" at10000)
    Assert.Empty(messages "new.question.errors" at10000)
    Assert.Empty(messages "new.title.errors" at10000)
    let over = Start.view (s [ Start.ProblemEvent, String('p', 10001); Start.QuestionEvent, String('q', 10001); Start.TitleEvent, String('t', 201) ])
    Assert.Equal<string list>([ "Problem statement must be at most 10,000 characters." ], messages "new.problem.errors" over)
    Assert.Equal<string list>([ "Primary investigative question must be at most 10,000 characters." ], messages "new.question.errors" over)
    Assert.Equal<string list>([ "Title must be at most 200 characters." ], messages "new.title.errors" over)
    // INV-006: the identifier must be a UUID; an empty one fails both of its rules, and every failed rule is listed.
    let badId = Start.view (s [ Start.IdentifierEvent, ""; Start.AttemptedKey, "1" ])
    Assert.Equal(2, (messages "new.identifier.errors" badId).Length)
    Assert.Equal(3, (messages "new.summary" badId).Length)
    Assert.Equal("The investigation was not started. 3 fields need attention.", text "new.refusal" badId)
    Assert.Equal(1, (messages "new.identifier.errors" (Start.view (s [ Start.IdentifierEvent, "not-a-uuid" ]))).Length)
    // The whole-form check integration gates CreateRequested on.
    Assert.Empty(Start.validateForm { ProblemStatement = "x"; Title = ""; Question = ""; Identifier = guid })
    Assert.Equal<Rule list>([ MinLength 1 ], Start.validateForm { ProblemStatement = " "; Title = ""; Question = ""; Identifier = guid } |> List.map (fun e -> e.Rule))
    // INV-002 is optional: the title falls back to the problem statement (causa-storage requires a non-blank title).
    Assert.Equal("Pump P-101 tripped", Start.titleOrFallback { ProblemStatement = " Pump P-101 tripped "; Title = " "; Question = ""; Identifier = guid })
    Assert.Equal(None, Start.question { ProblemStatement = "x"; Title = ""; Question = "  "; Identifier = guid })

    // INV-004, INV-005: Start is offered only on a writable connection with a working copy, and the destination names
    // the repository, the working copy and the branch; otherwise the engine's reason is shown.
    let (Workspace wc) = Ui2Scenarios.workingCopy.Workspace
    Assert.False(flag "new.start.disabled" fresh)
    Assert.Equal($"GitHub: harness/causa-test · {wc} on branch main", text "new.destination" fresh)
    let readOnly = Start.view (Ui2Scenarios.stateAt (Ui2Scenarios.connectedModel false "#/new") [])
    Assert.True(flag "new.start.disabled" readOnly)
    Assert.Contains("read-only", text "new.start.reason" readOnly)
    let disconnected = Start.view (Ui2Scenarios.stateAt (Update.initialModel (Routes.route "#/new")) [])
    Assert.True(flag "new.start.disabled" disconnected)
    Assert.False(flag "new.destination.writable" disconnected)

    // ---- workspace frame and details over the engine's session (CreateRequested -> unsaved StartInvestigation) -----
    let ws = Ui2Scenarios.newUnsaved "Pump trip" "Pump P-101 tripped" "Why did it trip?" EvidenceSection
    let frame = Frame.view ws
    Assert.True(flag "investigation.found" frame)
    Assert.Equal("Pump trip", text "investigation.title" frame)
    Assert.Equal("6f1c2a8e-3b7d-4c55-9a10-2e4f5d6c7b81", text "investigation.identifier" frame)
    Assert.Equal("Pump P-101 tripped", text "investigation.problem" frame)
    Assert.Equal("Why did it trip?", text "investigation.question" frame)
    Assert.Equal("Draft", text "investigation.state" frame) // the kernel's lifecycle after InvestigationStarted
    Assert.Equal("Not on GitHub until the first commit", text "investigation.created" frame) // INV-007 is never fabricated before a save
    Assert.Equal("2", text "investigation.version" frame)
    // UI-029: the five section links, each the canonical route, the current one marked aria-current="page".
    for section in Frame.sections do
        let slug = UiRoutes.sectionSlug section
        Assert.Equal(Ui2Scenarios.workspaceHash section, text ($"investigation.section.{slug}.href") frame)
        Assert.Equal((if section = EvidenceSection then "page" else "false"), text ($"investigation.section.{slug}.current") frame)
        Assert.Equal(UiRoutes.parse (Ui2Scenarios.workspaceHash section), WorkspaceRoute({ Workspace = wc; Role = WorkspaceRole.WorkingCopy; Id = Ui2Scenarios.investigationId }, section))
    // A stored investigation shows its stored creation time (INV-007) and the same identity after a reload (INV-008).
    let stored = Ui2Scenarios.loaded (SaveState.Saved(VerifiedAt(Ui2Scenarios.verified [ Ui2Scenarios.startCommand "Pump trip" "Pump P-101 tripped" ]))) "Pump trip" "Pump P-101 tripped" "Why did it trip?"
    let storedFrame = Frame.view stored
    Assert.Equal("2026-11-06 12:00 UTC", text "investigation.created" storedFrame)
    Assert.Equal("Pump P-101 tripped", text "investigation.problem" storedFrame)
    Assert.Equal("Why did it trip?", text "investigation.question" storedFrame)
    let details = Details.rows stored |> List.map (fun (k, _, v) -> k, v) |> Map.ofList
    Assert.Equal("Pump P-101 tripped", details.["problem"])
    Assert.Equal("Why did it trip?", details.["question"])
    Assert.Equal("6f1c2a8e-3b7d-4c55-9a10-2e4f5d6c7b81", details.["identifier"])
    Assert.Equal("2026-11-06 12:00 UTC", details.["created"])
    // A command the kernel refuses (a second start) contributes nothing to the projection.
    let refused = Frame.working (Some (Ui2Scenarios.verified [ Ui2Scenarios.startCommand "Pump trip" "Pump P-101 tripped" ]).Investigation) [ { Ui2Scenarios.startCommand "Other" "Other" with CommandId = Guid.NewGuid() } ]
    Assert.Equal("Pump trip", refused.Value.Title.Value)
    // A route to an investigation the engine does not hold shows no identity and says why; the key set is unchanged.
    let missingState = Ui2Scenarios.stateAt (Ui2Scenarios.connectedModel true (Ui2Scenarios.workspaceHash Overview)) []
    let missing = Frame.view missingState
    Assert.False(flag "investigation.found" missing)
    Assert.True(flag "investigation.absent" missing)
    Assert.Equal("", text "investigation.title" missing)
    Assert.Equal<Set<string>>(keysOf frame, keysOf missing)
    Assert.Equal<Set<string>>(keysOf frame, keysOf (Frame.view (Ui2Scenarios.stateAt (Update.initialModel (Routes.route "#/")) [])))
    Assert.Empty(Details.rows missingState)
