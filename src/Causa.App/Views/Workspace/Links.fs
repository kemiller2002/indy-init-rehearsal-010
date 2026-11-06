// Owner: ui-3. Relating evidence to a hypothesis (INV-030): the evidence and hypothesis choices, the relationship
// kind (INV-031, a choice group with no option selected initially), the rationale (INV-038), and the "Link evidence to
// a hypothesis" and "Reassess" actions the engine offers. A reassessment is a new assessment of the same pair; the
// earlier one stays in the history (INV-035, Assessments.history). Rendered inside the Evidence section.
namespace Causa.App.Views.Workspace

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

[<RequireQualifiedAccess>]
module Links =
    let private p = "investigation.links."

    let evidence = "investigation.link.evidence"
    let hypothesis = "investigation.link.hypothesis"
    let kind = "investigation.link.kind"
    let rationale = "investigation.link.rationale"
    let relate = "investigation.link.relate"
    let reassess = "investigation.link.reassess"

    /// The flag that checks one kind option (`investigation.link.kind.<option>`).
    let kindChecked (option: string) = kind + "." + option

    let keys (draft: string -> string) (investigation: Investigation option) : (string * ViewValue) list =
        let analysis = investigation |> Option.map (Analysis.project Analysis.actor)
        let chosenEvidence, chosenHypothesis, chosenKind = draft evidence, draft hypothesis, draft kind
        let relateAvailability = analysis |> Option.map (fun a -> Analysis.action a "INV-030") |> Option.defaultValue NotOffered
        let assessments = analysis |> Option.map (fun a -> a.Relationships) |> Option.defaultValue []
        [ Bind.flag (p + "loaded") investigation.IsSome
          Bind.items
              (p + "evidenceOptions")
              [ for e in analysis |> Option.map (fun a -> a.Evidence) |> Option.defaultValue [] ->
                    [ Cell.text "id" e.Id
                      Cell.text "label" (e.Label |> Option.defaultValue e.Content)
                      Cell.flag "selected" (e.Id = chosenEvidence) ] ]
          Bind.items
              (p + "hypothesisOptions")
              [ for h in analysis |> Option.map (fun a -> a.Hypotheses) |> Option.defaultValue [] ->
                    [ Cell.text "id" h.Id; Cell.text "label" h.Statement; Cell.flag "selected" (h.Id = chosenHypothesis) ] ]
          Bind.flag (p + "evidenceUnchosen") (chosenEvidence = "")
          Bind.flag (p + "hypothesisUnchosen") (chosenHypothesis = "")
          SectionBindings.draft draft evidence
          SectionBindings.draft draft hypothesis
          SectionBindings.draft draft kind
          for option in Analysis.kindOptions do
              Bind.flag (kindChecked option) (chosenKind = option)
          SectionBindings.draft draft rationale
          Bind.flag (p + "history.empty") assessments.IsEmpty
          Bind.items
              (p + "history")
              [ for l in assessments ->
                    [ Cell.text "id" l.Id
                      Cell.text "evidence" l.EvidenceLabel
                      Cell.text "kind" (Words.kind l.Kind)
                      Cell.text "hypothesis" l.HypothesisStatement
                      Cell.text "rationale" l.Rationale
                      Cell.flag "current" l.Current
                      Cell.text "status" (if l.Current then "current" else "earlier assessment") ] ] ]
        @ SectionBindings.action relate relateAvailability
        @ SectionBindings.action reassess relateAvailability

    let view (state: AppState) : (string * ViewValue) list = keys (Drafts.get state) (Analysis.loaded state)
