// Owner: ui-3. Conclusions (INV-033) as the history records them, rendered in the Overview section. "Record a
// conclusion" (INV-032) has no command in the frozen contract (owner decision pending), so the engine never offers it
// and its form is rendered only if it ever does.
namespace Causa.App.Views.Workspace

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

[<RequireQualifiedAccess>]
module Conclusions =
    let private p = "investigation.conclusions."

    let statement = "investigation.conclusion.statement"
    let record = "investigation.conclusion.record"

    let keys (draft: string -> string) (investigation: Investigation option) : (string * ViewValue) list =
        let analysis = investigation |> Option.map (Analysis.project Analysis.actor)
        let items = analysis |> Option.map (fun a -> a.Conclusions) |> Option.defaultValue []
        [ Bind.flag (p + "empty") items.IsEmpty
          Bind.items
              (p + "items")
              [ for c in items ->
                    [ Cell.text "id" c.Id
                      Cell.text "statement" c.Statement
                      Cell.flag "hasResidual" c.ResidualUncertainty.IsSome
                      Cell.text "residual" (SectionBindings.optional c.ResidualUncertainty)
                      Cell.text "version" (c.RecordedAtVersion |> Option.map string |> SectionBindings.optional) ] ]
          SectionBindings.draft draft statement ]
        @ SectionBindings.action record (analysis |> Option.map (fun a -> Analysis.action a "INV-032") |> Option.defaultValue NotOffered)

    let view (state: AppState) : (string * ViewValue) list = keys (Drafts.get state) (Analysis.loaded state)
