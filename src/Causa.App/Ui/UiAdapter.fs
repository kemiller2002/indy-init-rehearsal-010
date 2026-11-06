// Owner: integration. Derives the shell's presentation (primary navigation, document title) from AppState. The
// screens' own projections over the engine are added here as their slices land (Understood.projected is called only
// from this file).
namespace Causa.App.Ui

open System
open Causa.Domain
open Causa.Persistence
open Causa.App
open Causa.Ui.Contracts

/// The current state of an investigation the engine holds: the version GitHub verified (SM-LOAD Loaded), with the
/// session's unsaved commands decided on it in order by the domain kernel (or, for a new investigation, from nothing).
/// The UI renders this; it never decides legality itself (UI-004).
[<RequireQualifiedAccess>]
module Working =
    /// The actor the projection decides as (the save machine decides again, as the tab's actor, when it saves).
    let actor: ActorContext = { ActorId = "causa-web" }

    /// The commands of a session not yet confirmed saved, in order.
    let pending (session: InvestigationSession option) : CommandEnvelope list =
        match session with
        | Some s -> s.Unsaved @ s.AfterInFlight
        | None -> []

    /// One command decided and evolved on the state; a command the kernel refuses leaves the state unchanged.
    let apply (at: DateTimeOffset) (current: Investigation option) (command: CommandEnvelope) : Investigation option =
        match Kernel.domain.Decide Policy.core actor current command with
        | Allowed payloads ->
            let (AggregateVersion v0) = current |> Option.map (fun s -> s.Version) |> Option.defaultValue AggregateVersion.zero
            let actorId = NonEmptyText.tryCreate actor.ActorId |> Option.get
            payloads
            |> List.indexed
            |> List.fold
                (fun (acc: Investigation option) (i, payload) ->
                    let (InvestigationId inv) = command.Investigation
                    let version = v0 + int64 i + 1L
                    let envelope: EventEnvelope =
                        { EventId = EventId(Canonical.eventId inv version command.CommandId)
                          Investigation = command.Investigation
                          Version = AggregateVersion version
                          Provenance =
                            { OccurredAt = at
                              Actor = { ActorId = actorId; Kind = ActorKind.Human }
                              CorrelationId = command.CommandId
                              FormatVersion = Events.formatVersion
                              SemanticVersion = Events.semanticVersion }
                          Payload = payload }
                    match acc with
                    | None when i > 0 -> None
                    | _ ->
                        match Kernel.domain.Evolve acc envelope with
                        | Ok next -> Some next
                        | Error _ -> acc)
                current
        | _ -> current

    let private matches (t: RouteTarget) (r: InvestigationRef) =
        r.Id = t.Id && r.Address.Workspace = Workspace t.Workspace

    /// The engine's identity for a route target: a session's, a load's, or a listed investigation's.
    let refOf (model: Model) (t: RouteTarget) : InvestigationRef option =
        model.Sessions |> Map.tryFindKey (fun r _ -> matches t r)
        |> Option.orElse (model.Loads |> Map.tryFindKey (fun r _ -> matches t r))
        |> Option.orElse (
            model.Listings
            |> Map.toList
            |> List.tryPick (fun (_, l) -> l.Investigations |> List.tryFind (fun i -> matches t i.Ref) |> Option.map (fun i -> i.Ref)))

    /// The verified investigation a load holds.
    let verified (model: Model) (r: InvestigationRef) : VerifiedInvestigation option =
        match Map.tryFind r model.Loads with
        | Some(Loaded v) -> Some v
        | _ -> None

    /// The investigation's current state: verified, then every pending command.
    let current (model: Model) (r: InvestigationRef) : Investigation option =
        let baseState = verified model r |> Option.map (fun v -> v.Investigation)
        let at = baseState |> Option.map (fun s -> s.UpdatedAt) |> Option.defaultValue DateTimeOffset.UnixEpoch
        pending (Map.tryFind r model.Sessions) |> List.fold (apply at) baseState

    /// The route's investigation reference, when the route names one the engine knows.
    let routeRef (model: Model) (route: UiRoute) : InvestigationRef option =
        match route with
        | WorkspaceRoute(t, _) -> refOf model t
        | _ -> None

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

    /// The page title for a route ("<screen> · Causa"), for the report page when `report`.
    let titleAt (route: UiRoute) (report: bool) : string =
        let screen =
            match route with
            | InvestigationsRoute -> "Investigations"
            | StartRoute -> "New investigation"
            | WorkspaceRoute _ when report -> "Report · Investigation"
            | WorkspaceRoute(_, section) -> UiRoutes.sectionLabel section + " · Investigation"
            | AdministrationRoute -> "Administration"
            | SettingsRoute -> "Settings"
            | NotFoundRoute _ -> "Page not found"
        screen + " · Causa"

    /// The page title for a route ("<screen> · Causa").
    let title (route: UiRoute) : string = titleAt route false

    /// The current state of the investigation the route names (verified, with this tab's unsaved commands applied),
    /// or None when the engine holds none for it.
    let investigation (state: AppState) : Investigation option =
        Working.routeRef state.Model state.Route |> Option.bind (Working.current state.Model)
