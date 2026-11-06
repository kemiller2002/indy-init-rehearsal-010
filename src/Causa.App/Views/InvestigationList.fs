// Owner: ui. The Investigations destination (UI-001, UI-020, UI-022, UI-023, UI-024): before Connect it says how to
// start; after it, every listed investigation with its title, where it lives, its actual lifecycle and counts when it
// has been opened and verified (never invented, never an unknown shown as zero), and an Open investigation link that
// keeps its identity (workspace and id) in the route. Keys start with "investigations.".
namespace Causa.App.Views

open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module InvestigationList =
    /// The sort control's event name (a draft): "title" (default) or "workspace".
    let sort = "investigations.sort"

    /// UI-024-O3 wording ("Opening …", not "Fetching and validating …"). Language.fs compiles after this file, so the
    /// list's own copy lives here.
    let notConnected = "Connect a data repository to list its investigations."
    let openingList = "Opening the list of investigations…"
    let noInvestigations = "This repository has no investigations yet."

    let sortOptions = [ "title", "Title"; "workspace", "Where it is stored" ]

    /// What a count says before the investigation has been opened: unknown, never zero (UI-022-O6).
    let unknownCount = "Not known until opened"

    type Row =
        { Key: string
          Title: string
          Where: string
          Status: string
          LastActivity: string
          Hypotheses: string
          Evidence: string
          Contradictions: string
          Href: string }

    let private rowOf (model: Model) (item: ListedInvestigation) : Row =
        let (Workspace workspace) = item.Ref.Address.Workspace
        let role = item.Ref.Address.Role
        let target = { Workspace = workspace; Role = role; Id = item.Ref.Id }
        let verified =
            match model.Loads.TryFind item.Ref with
            | Some(Loaded v) -> Some v.Investigation
            | _ -> None
        let count f = verified |> Option.map (f >> string) |> Option.defaultValue unknownCount
        let status =
            match model.Loads.TryFind item.Ref, verified with
            | _, Some s -> Causa.App.Workspace.Words.lifecycle s.Lifecycle |> fun w -> string (System.Char.ToUpperInvariant w[0]) + w.Substring 1
            | Some(Loading _), _ -> "Opening investigation"
            | Some(LoadFailed _), _ -> "Could not be opened"
            | _ -> "Not opened yet"
        { Key = workspace + "|" + Ids.text (Ids.investigation item.Ref.Id)
          Title = item.Title |> Option.defaultValue "Untitled investigation"
          Where = (if role = WorkspaceRole.WorkingCopy then "Your working copy" else "Catalog, read only")
          Status = status
          LastActivity = verified |> Option.map (fun s -> Causa.App.Workspace.Words.instant s.UpdatedAt) |> Option.defaultValue unknownCount
          Hypotheses = count (fun s -> s.Hypotheses.Count)
          Evidence = count (fun s -> s.Evidence.Count)
          Contradictions =
            count (fun s ->
                s.Contradictions
                |> Map.filter (fun _ c -> c.State <> ContradictionState.Resolved && c.State <> ContradictionState.Superseded)
                |> Map.count)
          Href = UiRoutes.href (WorkspaceRoute(target, Overview)) }

    let rows (model: Model) (sortBy: string) : Row list =
        model.Listings
        |> Map.toList
        |> List.collect (fun (_, listing) -> listing.Investigations |> List.map (rowOf model))
        |> List.distinctBy (fun r -> r.Key)
        |> if sortBy = "workspace" then List.sortBy (fun r -> r.Where, r.Title) else List.sortBy (fun r -> r.Title, r.Where)

    let view (state: AppState) : (string * ViewValue) list =
        let connected = match state.Model.Connection with Connected _ -> true | _ -> false
        let sortBy = match Drafts.get state sort with "workspace" -> "workspace" | _ -> "title"
        let items = rows state.Model sortBy
        let listing = not state.Model.ListingsInFlight.IsEmpty
        let listed = not state.Model.Listings.IsEmpty
        [ Bind.text "investigations.heading" "Investigations"
          Bind.flag "investigations.connected" connected
          Bind.text "investigations.status" (if connected then "" else notConnected)
          Bind.flag "investigations.loading" listing
          Bind.text "investigations.loadingText" openingList
          Bind.flag "investigations.empty" (connected && listed && not listing && items.IsEmpty)
          Bind.text "investigations.emptyText" noInvestigations
          Bind.flag "investigations.hasItems" (not items.IsEmpty)
          Bind.text "investigations.count" (if items.IsEmpty then "" else $"{items.Length} investigation(s)")
          Bind.text sort sortBy
          Bind.items
              "investigations.sortOptions"
              [ for id, label in sortOptions -> [ Cell.text "id" id; Cell.text "label" label; Cell.flag "selected" (id = sortBy) ] ]
          Bind.items
              "investigations.items"
              [ for r in items ->
                    [ Cell.text "id" r.Key
                      Cell.text "title" r.Title
                      Cell.text "where" r.Where
                      Cell.text "status" r.Status
                      Cell.text "lastActivity" r.LastActivity
                      Cell.text "hypotheses" r.Hypotheses
                      Cell.text "evidence" r.Evidence
                      Cell.text "contradictions" r.Contradictions
                      Cell.text "href" r.Href ] ] ]
