// Owner: ui-2. The investigation details region of the workspace Overview (workspace-details fragment): the identity
// and the content the investigation was started with, as the domain kernel projects them (INV-006, INV-007, INV-008,
// INV-018, INV-023). Read-only: causa-storage v1.0.0 has no rename or detail-revision event, so no edit is offered.
namespace Causa.App.Views

open Causa.Domain
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Details =
    /// One labelled detail (a row of the key-value list): its key, label and value.
    let rows (state: AppState) : (string * string * string) list =
        match Frame.locate state with
        | None -> []
        | Some located ->
            match Frame.investigation located with
            | None -> []
            | Some w ->
                let i = w.Investigation
                let (InvestigationId id) = i.Id
                [ "identifier", "Investigation identifier", id.ToString("D")
                  "title", "Title", i.Title.Value
                  "problem", "Original problem statement", Frame.problemStatement i |> Option.defaultValue ""
                  "question", "Primary investigative question", Frame.question i |> Option.defaultValue "None recorded"
                  "state", "Investigation state", Frame.lifecycle i.Lifecycle
                  "created", "Creation timestamp", Frame.createdAt located |> Option.map Frame.formatTime |> Option.defaultValue "Not on GitHub until the first commit" ]

    /// The details keys: a data-each list (key, label, value) and whether there is anything to show.
    let view (state: AppState) : (string * ViewValue) list =
        let details = rows state
        [ Bind.flag "investigation.details.shown" (not details.IsEmpty)
          Bind.items
              "investigation.details.items"
              (details |> List.map (fun (key, label, value) -> [ Cell.text "key" key; Cell.text "label" label; Cell.text "value" value ])) ]
