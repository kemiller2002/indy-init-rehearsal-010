// Owner: ui-3. The Causal Analysis section (screen.investigation.causal-analysis): the accessible text relationship
// list, the UI-026 P0 floor. It lists every evidence-hypothesis assessment (current and earlier) and every causal
// assertion with the state the domain gives it, each linked to its records (UI-033). It is read-only: nothing here
// changes the domain (UI-026-O4). No drawn graph yet (the WDL workflow layout is not part of this build).
namespace Causa.App.Views

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace
open Causa.App.Views.Workspace

/// WDL-003, WDL-004: the typed adapter from the domain to the Forma portable workflow format. Causa owns what the graph
/// means (this module: nodes for hypotheses and causal-assertion outcomes, edges for cause roles, evidence counts); the
/// Forma workflow engine (`<forma-workflow>`, @echelon-foundry/forma-workflow) owns placement, routing and spacing (no
/// coordinate is ever written: layout mode "auto"); Forma owns the visual components. The projection reads the
/// kernel's state only: it records no claim, changes no state and infers nothing (WDL-003-O2).
[<RequireQualifiedAccess>]
module CausalGraph =
    type NodeKind =
        | HypothesisNode
        | OutcomeNode

    type Node =
        { Id: string
          Kind: NodeKind
          Label: string
          Description: string
          /// The domain's own state word (hypothesis state or cause state).
          State: string
          StateKey: string
          RecordId: string
          Href: string
          Supporting: int
          Contradicting: int
          Other: int }

    type Edge =
        { Id: string
          Source: string
          Target: string
          Role: CauseRole
          RoleLabel: string }

    type Graph = { Title: string; Nodes: Node list; Edges: Edge list }

    /// The Forma text limit for a label (1000); the whole text stays in the description.
    let private label (text: string) = if text.Length <= 1000 then text else text.Substring(0, 999) + "…"

    let private key (word: string) = word.Replace(' ', '-')

    /// The line that distinguishes each cause role without colour (WDL-007-O2); the legend says so in words.
    let line (role: CauseRole) =
        match role with
        | CauseRole.RootCause -> "solid"
        | CauseRole.Trigger -> "solid"
        | CauseRole.ContributingFactor -> "dashed"
        | CauseRole.EnablingCondition -> "dotted"

    let project (href: Section -> string option -> string) (s: Investigation) : Graph =
        let assessments = Assessments.current s
        let hypothesisNodes =
            s.Hypotheses
            |> Map.toList
            |> List.sortBy (fun (_, h) -> AggregateVersion.value h.Provenance.AtVersion, Ids.hypothesis h.Hypothesis.HypothesisId)
            |> List.map (fun (id, h) ->
                let mine = assessments |> List.filter (fun r -> r.Value.HypothesisId = id)
                let count kind = mine |> List.filter (fun r -> r.Value.Kind = kind) |> List.length
                let supporting, contradicting = count Causa.Domain.RelationshipKind.Supports, count Causa.Domain.RelationshipKind.Contradicts
                let other = mine.Length - supporting - contradicting
                let record = Ids.text (Ids.hypothesis id)
                let state = Words.hypothesisState h.State
                { Id = "h-" + record
                  Kind = HypothesisNode
                  Label = label h.Hypothesis.Statement.Value
                  Description =
                    $"Evidence: {supporting} supporting, {contradicting} contradicting"
                    + (if other > 0 then $", {other} other assessment(s)" else "")
                    + (if h.Hypothesis.Statement.Value.Length > 1000 then $". {h.Hypothesis.Statement.Value}" else "")
                  State = state
                  StateKey = key state
                  RecordId = record
                  Href = href HypothesesSection (Some record)
                  Supporting = supporting
                  Contradicting = contradicting
                  Other = other })
        let causes =
            s.Causes |> Map.toList |> List.sortBy (fun (_, c) -> AggregateVersion.value c.Provenance.AtVersion, Ids.cause c.Cause.CauseId)
        let outcomeNodes =
            causes
            |> List.map (fun (id, c) ->
                let record = Ids.text (Ids.cause id)
                let state = Words.causeState c.State
                { Id = "c-" + record
                  Kind = OutcomeNode
                  Label = label c.Cause.Outcome.Value
                  Description = c.Cause.Mechanism |> Option.map (fun m -> $"Mechanism: {m.Value}") |> Option.defaultValue "Mechanism not recorded"
                  State = state
                  StateKey = key state
                  RecordId = record
                  Href = href CausalAnalysis None
                  Supporting = 0
                  Contradicting = 0
                  Other = 0 })
        let edges =
            causes
            |> List.collect (fun (id, c) ->
                c.Cause.Members
                |> List.map (fun m ->
                    let cause, hyp = Ids.text (Ids.cause id), Ids.text (Ids.hypothesis m.HypothesisId)
                    { Id = $"m-{cause}-{hyp}"; Source = "h-" + hyp; Target = "c-" + cause; Role = m.Role; RoleLabel = Words.causeRole m.Role }))
            |> List.distinctBy (fun e -> e.Id)
        { Title = $"Causal graph: {s.Title.Value}"; Nodes = hypothesisNodes @ outcomeNodes; Edges = edges }

    /// The legend: what each line and status means, in words (WDL-007-O2).
    let legend =
        [ "root-cause", "root cause", "solid"
          "trigger", "trigger", "solid"
          "contributing-factor", "contributing factor", "dashed"
          "enabling-condition", "enabling condition", "dotted" ]

    /// The Forma portable workflow document (format 1.0.0) for a graph: no coordinates, so Forma lays it out.
    let toWorkflow (g: Graph) : string =
        use stream = new System.IO.MemoryStream()
        (
            use w = new System.Text.Json.Utf8JsonWriter(stream)
            let str (name: string) (value: string) = w.WriteString(name, value)
            w.WriteStartObject()
            str "format" "forma-workflow"
            str "formatVersion" "1.0.0"
            str "id" "causal-graph"
            str "title" (label g.Title)
            str "description" "Hypotheses and the outcomes of causal assertions, linked by cause role, with evidence counts."
            w.WriteStartObject "forma"
            str "version" "0.4.0"
            w.WriteEndObject()
            w.WriteStartObject "layout"
            str "direction" "right"
            str "mode" "auto"
            w.WriteEndObject()
            w.WriteStartObject "presentation"
            str "legendTitle" "Cause roles"
            w.WriteStartArray "legend"
            for id, words, ln in legend do
                w.WriteStartObject()
                str "id" id
                str "label" words
                str "description" $"A {ln} line from a hypothesis to an outcome: the hypothesis is its {words}."
                str "line" ln
                w.WriteEndObject()
            w.WriteEndArray()
            w.WriteEndObject()
            w.WriteStartArray "nodes"
            for n in g.Nodes do
                w.WriteStartObject()
                str "id" n.Id
                match n.Kind with
                | HypothesisNode ->
                    str "kind" "org.causa:hypothesis"
                    str "kindLabel" "Hypothesis"
                    str "variant" "rounded"
                | OutcomeNode ->
                    str "kind" "org.causa:outcome"
                    str "kindLabel" "Outcome"
                    str "variant" "rectangle"
                str "label" n.Label
                str "description" n.Description
                w.WriteStartObject "status"
                str "state" ("org.causa:" + n.StateKey)
                str "label" n.State
                w.WriteEndObject()
                // Colour repeats the domain's own state only (WDL-008-O2): a confirmed cause, a falsified hypothesis.
                match n.Kind, n.StateKey with
                | OutcomeNode, "confirmed" ->
                    w.WriteStartObject "color"
                    str "stroke" "token:status-success"
                    w.WriteEndObject()
                | HypothesisNode, "falsified" ->
                    w.WriteStartObject "color"
                    str "stroke" "token:status-danger"
                    w.WriteEndObject()
                | _ -> ()
                w.WriteStartArray "references"
                w.WriteStartObject()
                str "id" "record"
                str "system" "org.causa"
                str "type" (match n.Kind with HypothesisNode -> "Hypothesis" | OutcomeNode -> "CausalAssertion")
                str "key" n.RecordId
                str "label" (match n.Kind with HypothesisNode -> "Open the hypothesis" | OutcomeNode -> "Open the causal analysis")
                if n.Href <> "" then str "href" n.Href
                w.WriteEndObject()
                w.WriteEndArray()
                w.WriteStartObject "metadata"
                w.WriteNumber("supporting", n.Supporting)
                w.WriteNumber("contradicting", n.Contradicting)
                w.WriteEndObject()
                w.WriteStartObject "accessibility"
                let kindWord = match n.Kind with HypothesisNode -> "Hypothesis" | OutcomeNode -> "Outcome"
                str "name" (label $"{kindWord} ({n.State}): {n.Label}")
                w.WriteEndObject()
                w.WriteEndObject()
            w.WriteEndArray()
            w.WriteStartArray "edges"
            for e in g.Edges do
                w.WriteStartObject()
                str "id" e.Id
                w.WriteStartObject "source"
                str "node" e.Source
                w.WriteEndObject()
                w.WriteStartObject "target"
                str "node" e.Target
                w.WriteEndObject()
                str "kind" ("org.causa:" + (Words.causeRole e.Role).Replace(' ', '-'))
                str "kindLabel" e.RoleLabel
                str "label" e.RoleLabel
                str "line" (line e.Role)
                w.WriteEndObject()
            w.WriteEndArray()
            w.WriteEndObject()
        )
        System.Text.Encoding.UTF8.GetString(stream.ToArray())

[<RequireQualifiedAccess>]
module Graph =
    let private p = "investigation.causal."

    let render (ctx: SectionContext) : (string * ViewValue) list =
        let investigation = ctx.Investigation
        let relationships = investigation |> Option.map Analysis.links |> Option.defaultValue []
        let graph = investigation |> Option.map (CausalGraph.project ctx.Href)
        let causes =
            investigation
            |> Option.map (fun s ->
                s.Causes
                |> Map.toList
                |> List.map snd
                |> List.sortBy (fun c -> AggregateVersion.value c.Provenance.AtVersion)
                |> List.map (fun c ->
                    [ Cell.text "id" (Ids.text (Ids.cause c.Cause.CauseId))
                      Cell.text "outcome" c.Cause.Outcome.Value
                      Cell.text "state" (Words.causeState c.State)
                      Cell.text
                          "members"
                          (c.Cause.Members
                           |> List.map (fun m -> $"{Analysis.hypothesisStatement s m.HypothesisId} ({Words.causeRole m.Role})")
                           |> String.concat "; ") ]))
            |> Option.defaultValue []
        SectionBindings.status p ctx
        @ [ Bind.flag (p + "relationships.empty") relationships.IsEmpty
            Bind.items
                (p + "relationships")
                [ for l in relationships ->
                      [ Cell.text "id" l.Id
                        Cell.text "evidence" l.EvidenceLabel
                        Cell.text "evidenceHref" (RecordLinks.href ctx l.Evidence)
                        Cell.text "kind" (Words.kind l.Kind)
                        Cell.text "hypothesis" l.HypothesisStatement
                        Cell.text "hypothesisHref" (RecordLinks.href ctx l.Hypothesis)
                        Cell.text "rationale" l.Rationale
                        Cell.flag "current" l.Current
                        Cell.text "status" (if l.Current then "current" else "earlier assessment") ]
                      @ SectionBindings.recordCells ctx { Kind = RelationshipEntity; Id = l.Id } ]
            Bind.flag (p + "causes.empty") causes.IsEmpty
            Bind.items (p + "causes") causes
            // The drawn graph (UI-026, WDL): the workflow document the Forma workflow engine lays out and renders.
            Bind.flag (p + "graph.shown") graph.IsSome
            Bind.flag (p + "graph.empty") (graph |> Option.exists (fun g -> g.Nodes.IsEmpty))
            Bind.text (p + "graph.title") (graph |> Option.map (fun g -> g.Title) |> SectionBindings.optional)
            Bind.text (p + "graph.summary") (graph |> Option.map (fun g -> $"{g.Nodes.Length} node(s), {g.Edges.Length} cause link(s)") |> SectionBindings.optional)
            Bind.text (p + "graph.workflow") (graph |> Option.map CausalGraph.toWorkflow |> SectionBindings.optional) ]

    let keys (investigation: Investigation option) = render (SectionContext.ofInvestigation investigation)

    let view (state: AppState) : (string * ViewValue) list = render (SectionContext.ofState state)
