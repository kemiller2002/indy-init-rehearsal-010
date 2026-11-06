// Owner: ui-3. The Hypotheses section (screen.investigation.hypotheses): every hypothesis with its statement
// (INV-026), state, explanation (INV-011) and the evidence currently assessed against it (INV-044, J2-05), and the
// "Record a hypothesis" form (INV-009) the engine offers.
namespace Causa.App.Views.Workspace

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

[<RequireQualifiedAccess>]
module Hypotheses =
    let private p = "investigation.hypotheses."

    let statement = "investigation.hypothesis.statement"
    let explanation = "investigation.hypothesis.explanation"
    let record = "investigation.hypothesis.record"

    /// One current assessment as the hypothesis shows it: kind, evidence and rationale.
    let linkLine (l: LinkCard) = $"{Words.kind l.Kind}: {l.EvidenceLabel} — {l.Rationale}"

    let rows (ctx: SectionContext) (analysis: AnalysisProjection) : (string * ViewPrimitive) list list =
        [ for h in analysis.Hypotheses ->
              [ Cell.text "id" h.Id
                Cell.text "statement" h.Statement
                Cell.text "state" h.State
                Cell.flag "hasExplanation" h.Explanation.IsSome
                Cell.text "explanation" (SectionBindings.optional h.Explanation)
                Cell.flag "hasLinks" (not h.Links.IsEmpty)
                Cell.int "linkCount" h.Links.Length
                Cell.text "links" (h.Links |> List.map linkLine |> String.concat "\n") ]
              @ SectionBindings.recordCells ctx { Kind = HypothesisEntity; Id = h.Id } ]

    let render (draft: string -> string) (ctx: SectionContext) : (string * ViewValue) list =
        let analysis = ctx.Investigation |> Option.map (Analysis.project Analysis.actor)
        let items = analysis |> Option.map (rows ctx) |> Option.defaultValue []
        SectionBindings.status p ctx
        @ [ Bind.flag (p + "empty") items.IsEmpty
            Bind.items (p + "items") items
            SectionBindings.draft draft statement
            SectionBindings.draft draft explanation ]
        @ SectionBindings.action record (analysis |> Option.map (fun a -> Analysis.action a "INV-009") |> Option.defaultValue NotOffered)

    let keys (draft: string -> string) (investigation: Investigation option) = render draft (SectionContext.ofInvestigation investigation)

    let view (state: AppState) : (string * ViewValue) list = render (Drafts.get state) (SectionContext.ofState state)
