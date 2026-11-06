// Owner: integration. The engine: pure Init and Update over the frozen IF41-APP Model, and the integration state
// (AppState) around it. Messages owned by later slices (connection, listing, load, save, conflict, domain commands)
// leave the model unchanged until those slices wire their machines in here.
namespace Causa.App

open Causa.Persistence
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Update =
    /// The model a tab starts with: disconnected, online, no sessions; request identities start at 1.
    let initialModel (route: Route) : Model =
        { Connection = Disconnected
          Context = None
          NextRequestId = RequestId 1L
          Network = NetworkState.Online
          Route = route
          Loads = Map.empty
          Sessions = Map.empty
          Listings = Map.empty
          ListingsInFlight = Map.empty }

    /// One frozen IF41-APP message: the next model and the effects the host performs.
    let step (msg: Msg) (model: Model) : Model * HostEffect list =
        match msg with
        | Navigate route -> { model with Route = route }, []
        | Network NetworkInput.OfflineObserved
        | Browser(ConnectivityChanged false) -> { model with Network = NetworkState.Offline }, []
        | Network NetworkInput.OnlineObserved
        | Browser(ConnectivityChanged true) -> { model with Network = NetworkState.Online }, []
        | Browser(TimerFired _)
        | Browser(HttpCompleted _)
        | Browser LeaveAttempted
        | Connection _
        | SessionInput _
        | ConflictStep _
        | LoadStep _
        | ListRequested _
        | CreateRequested _
        | DomainCommand _ -> model, []

    /// The frozen IF41-APP engine value.
    let engine: Engine =
        { Init = fun route -> initialModel route, []
          Update = step }

    let private withModel (state: AppState) (model: Model, effects: HostEffect list) : AppState * AppEffect list =
        let announcements = effects |> List.choose (function LiveAnnounce(p, text) -> Some(p, text) | _ -> None)
        let rest = effects |> List.filter (function LiveAnnounce _ -> false | _ -> true) |> List.map HostEffect
        { state with
            Model = model
            Route = UiRoutes.parse (Routes.hashOf model.Route)
            Announcements = announcements },
        rest

    /// The state a page load starts from, at the location hash the browser reported.
    let init (hash: string) : AppState * AppEffect list =
        // A page loaded at an in-page anchor (e.g. "#main" after the skip link) starts at the root route.
        let hash = if hash.Length > 1 && not (hash.StartsWith "#/") then "" else hash
        let route = Routes.route hash
        let model, effects = engine.Init route
        withModel
            { Model = model
              Route = UiRoutes.parse (Routes.hashOf route)
              Drafts = Map.empty
              Credential = None
              Announcements = [] }
            (model, effects)

    let update (msg: AppMsg) (state: AppState) : AppState * AppEffect list =
        match msg with
        | EngineMsg(Navigate route as m) ->
            let next, effects = withModel state (engine.Update m state.Model)
            let hash = Routes.hashOf route
            // A navigation the engine asked for moves the browser too (Limen pushes same-origin URLs only).
            next, effects @ [ PushLocation hash ]
        | EngineMsg m -> withModel state (engine.Update m state.Model)
        // An in-page anchor (a hash that is not a route, e.g. "#main") does not change the route.
        | LocationChanged hash when hash.Length > 1 && not (hash.StartsWith "#/") -> { state with Announcements = [] }, []
        | LocationChanged hash -> withModel state (engine.Update (Navigate(Routes.route hash)) state.Model)
        | DraftChanged(key, value) -> { state with Drafts = Map.add key value state.Drafts; Announcements = [] }, []
        | CredentialEntered credential -> { state with Credential = Some credential; Announcements = [] }, []
