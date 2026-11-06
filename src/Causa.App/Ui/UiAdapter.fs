// Owner: integration. Derives the shell's presentation (primary navigation, document title) from AppState. The
// screens' own projections over the engine are added here as their slices land (Understood.projected is called only
// from this file).
namespace Causa.App.Ui

open Causa.App
open Causa.Ui.Contracts

[<RequireQualifiedAccess>]
module UiAdapter =
    /// The primary destinations (UI-020; design contract primaryDestinations), in order.
    let destinations: (PrimaryDestination * string * UiRoute) list =
        [ Investigations, "Investigations", InvestigationsRoute
          Administration, "Administration", AdministrationRoute
          Settings, "Settings", SettingsRoute ]

    let destinationSlug (d: PrimaryDestination) =
        match d with
        | Investigations -> "investigations"
        | Administration -> "administration"
        | Settings -> "settings"

    /// Which primary destination a route belongs to: the list, start and workspace screens are under Investigations.
    let destinationOf (route: UiRoute) : PrimaryDestination option =
        match route with
        | InvestigationsRoute
        | StartRoute
        | WorkspaceRoute _ -> Some Investigations
        | AdministrationRoute -> Some Administration
        | SettingsRoute -> Some Settings
        | NotFoundRoute _ -> None

    let primary (route: UiRoute) : NavLink<PrimaryDestination> list =
        destinations
        |> List.map (fun (d, label, r) ->
            { Destination = d; Label = label; Href = UiRoutes.href r; Current = (destinationOf route = Some d) })

    /// The page title for a route ("<screen> · Causa").
    let title (route: UiRoute) : string =
        let screen =
            match route with
            | InvestigationsRoute -> "Investigations"
            | StartRoute -> "New investigation"
            | WorkspaceRoute(_, section) -> UiRoutes.sectionLabel section + " · Investigation"
            | AdministrationRoute -> "Administration"
            | SettingsRoute -> "Settings"
            | NotFoundRoute _ -> "Page not found"
        screen + " · Causa"
