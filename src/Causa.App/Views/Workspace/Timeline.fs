// Owner: ui-3. The Timeline section (screen.investigation.timeline): every stored event of this investigation in
// version order, with its time, actor and a summary in the domain's words, so a reassessment shows both the earlier
// and the new kind (INV-035, J2-06).
namespace Causa.App.Views.Workspace

open Causa.Domain
open Causa.Ui.Contracts
open Causa.App.Ui
open Causa.App.Workspace

[<RequireQualifiedAccess>]
module Timeline =
    let private p = "investigation.timeline."

    let keys (investigation: Investigation option) : (string * ViewValue) list =
        let items =
            investigation
            |> Option.map (fun s -> Analysis.timeline (Kernel.domain.Project s))
            |> Option.defaultValue []
        [ Bind.flag (p + "loaded") investigation.IsSome
          Bind.flag (p + "missing") investigation.IsNone
          Bind.text (p + "notLoaded") SectionBindings.notLoaded
          Bind.flag (p + "empty") items.IsEmpty
          Bind.items
              (p + "items")
              [ for t in items ->
                    [ Cell.text "id" (string t.Version)
                      Cell.text "version" (string t.Version)
                      Cell.text "eventType" t.EventType
                      Cell.text "at" (Words.instant t.OccurredAt)
                      Cell.text "actor" (SectionBindings.optional t.Actor)
                      Cell.text "summary" t.Summary ] ] ]

    let view (state: AppState) : (string * ViewValue) list = keys (Analysis.loaded state)
