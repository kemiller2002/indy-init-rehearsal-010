// Owner: ui-3. The Causal Analysis section (screen.investigation.causal-analysis): the accessible text relationship
// list, the UI-026 P0 floor. It lists every evidence-hypothesis assessment (current and earlier) and every causal
// assertion with the state the domain gives it; no graph is drawn.
namespace Causa.App.Views

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace
open Causa.App.Views.Workspace

[<RequireQualifiedAccess>]
module Graph =
    let private p = "investigation.causal."

    let keys (investigation: Investigation option) : (string * ViewValue) list =
        let relationships = investigation |> Option.map Analysis.links |> Option.defaultValue []
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
        [ Bind.flag (p + "loaded") investigation.IsSome
          Bind.flag (p + "missing") investigation.IsNone
          Bind.text (p + "notLoaded") SectionBindings.notLoaded
          Bind.flag (p + "relationships.empty") relationships.IsEmpty
          Bind.items
              (p + "relationships")
              [ for l in relationships ->
                    [ Cell.text "id" l.Id
                      Cell.text "evidence" l.EvidenceLabel
                      Cell.text "kind" (Words.kind l.Kind)
                      Cell.text "hypothesis" l.HypothesisStatement
                      Cell.text "rationale" l.Rationale
                      Cell.flag "current" l.Current
                      Cell.text "status" (if l.Current then "current" else "earlier assessment") ] ]
          Bind.flag (p + "causes.empty") causes.IsEmpty
          Bind.items (p + "causes") causes ]

    let view (state: AppState) : (string * ViewValue) list = keys (Analysis.loaded state)
