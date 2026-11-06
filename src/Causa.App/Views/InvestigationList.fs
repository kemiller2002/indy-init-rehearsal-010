// Owner: ui (written by the integration lane acting as ui for T17, change request 5 of ui-2). The Investigations
// destination (UI-001, UI-020): the investigations of the active catalog and of this application's working copy, as
// the engine listed them, each opening its workspace; and the New investigation action while a writable working copy
// is listed. Keys start with "investigations.".
namespace Causa.App.Views

open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module InvestigationList =
    type Row = { Key: string; Title: string; Href: string; Kind: string; KindState: string; Note: string }

    let private targetOf (r: InvestigationRef) : RouteTarget =
        let (Workspace w) = r.Address.Workspace
        { Workspace = w; Role = r.Address.Role; Id = r.Id }

    let private row (model: Model) (r: InvestigationRef) (listedTitle: string option) : Row =
        let (InvestigationId g) = r.Id
        let (Workspace w) = r.Address.Workspace
        let current = Working.current model r
        let title =
            current |> Option.map (fun i -> i.Title.Value)
            |> Option.orElse listedTitle
            |> Option.defaultValue (g.ToString("D"))
        let note =
            match Map.tryFind r model.Loads, Map.tryFind r model.Sessions with
            | _, Some s when not (Working.pending (Some s)).IsEmpty -> "Unsaved changes in this tab"
            | Some(Loading _), _ -> "Opening…"
            | Some(LoadFailed _), _ -> "Could not be opened"
            | _ -> ""
        let kind, kindState =
            match r.Address.Role with
            | WorkspaceRole.Catalog -> "Frozen demonstration (read-only)", "frozen"
            | WorkspaceRole.WorkingCopy -> "Working copy", "ok"
        { Key = w + "/" + g.ToString("D")
          Title = title
          Href = UiRoutes.href (WorkspaceRoute(targetOf r, Overview))
          Kind = kind
          KindState = kindState
          Note = note }

    /// Every investigation the engine holds: the working copy first (this tab's sessions included), then the catalog.
    let rows (model: Model) : Row list =
        let listed =
            model.Listings
            |> Map.toList
            |> List.sortBy (fun (a, _) -> if a.Role = WorkspaceRole.WorkingCopy then 0 else 1)
            |> List.collect (fun (_, l) -> l.Investigations |> List.map (fun i -> i.Ref, i.Title))
        let known = listed |> List.map fst |> Set.ofList
        let created = model.Sessions |> Map.toList |> List.map fst |> List.filter (known.Contains >> not) |> List.map (fun r -> r, None)
        created @ listed |> List.map (fun (r, t) -> row model r t)

    let canStart (model: Model) =
        match model.Connection with
        | Connected true -> model.Listings |> Map.exists (fun a _ -> a.Role = WorkspaceRole.WorkingCopy)
        | _ -> false

    let view (state: AppState) : (string * ViewValue) list =
        let model = state.Model
        let connected = match model.Connection with Connected _ -> true | _ -> false
        let rows = rows model
        let status =
            match model.Connection with
            | Connected _ when rows.IsEmpty && not model.ListingsInFlight.IsEmpty -> "Listing investigations…"
            | Connected _ when rows.IsEmpty -> "This repository holds no investigations yet."
            | Connected _ -> ""
            | _ -> "Connect a data repository to list its investigations."
        [ Bind.text "investigations.heading" "Investigations"
          Bind.flag "investigations.connected" connected
          Bind.text "investigations.status" status
          Bind.flag "investigations.can-start" (canStart model)
          Bind.flag "investigations.any" (not rows.IsEmpty)
          Bind.items
              "investigations.items"
              [ for r in rows ->
                    [ Cell.text "key" r.Key
                      Cell.text "title" r.Title
                      Cell.text "href" r.Href
                      Cell.text "kind" r.Kind
                      Cell.text "kindState" r.KindState
                      Cell.text "note" r.Note ] ] ]
