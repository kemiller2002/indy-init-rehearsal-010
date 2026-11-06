// Owner: integration. The engine: pure Init and Update over the frozen IF41-APP Model (SM-CONNECTION, SM-NETWORK,
// listing, SM-LOAD, SM-SAVE and SM-CONFLICT wired to the persistence machines and the domain kernel), and the
// integration state (AppState) around it. Every request is IF41-WIRE data turned into a Limen Http effect; every answer
// comes back as HttpCompleted and is routed by its RequestId to the one operation waiting for it.
namespace Causa.App

open System
open Causa.Domain
open Causa.Persistence
open Causa.App.Ui

/// What the host boundary supplies to one update: the clock and fresh identities (the engine stays pure).
type HostEnv = { Now: DateTimeOffset; Fresh: Guid list }

[<RequireQualifiedAccess>]
module Update =
    /// The working copy every tab of this application writes (CONTRACT section 9.4): workspaces/demo/causa-app-r10.
    /// It is fixed, so a fresh tab lists what another tab saved (J1-06).
    [<Literal>]
    let WorkingCopyRun = "r10"

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

    let private next (RequestId n) = RequestId(n + 1L)

    let private online (model: Model) = model.Network = NetworkState.Online

    let private idOf (outcome: HttpOutcome) =
        match outcome with
        | Answered(id, _, _, _) -> id
        | NotAnswered(id, _) -> id

    /// Whether a session other than `r` has a mutation in flight (T-S04, T-S53).
    let private mutationElsewhere (model: Model) (r: InvestigationRef option) =
        model.Sessions
        |> Map.exists (fun k s ->
            Some k <> r
            && (match s.Outstanding with
                | Some(_, MutationAnswer) -> true
                | _ -> false))

    let private contextFor (now: DateTimeOffset) (model: Model) (r: InvestigationRef option) : RequestContext option =
        model.Context
        |> Option.map (fun c ->
            { Connection = c
              NextId = model.NextRequestId
              MutationInFlightElsewhere = mutationElsewhere model r
              Now = now })

    /// The host effects of machine effects. Each request sent advances the tab's next request identity.
    let private effectsOf (model: Model) (effects: SaveEffect list) : Model * HostEffect list =
        effects
        |> List.fold
            (fun (m: Model, acc) effect ->
                match effect with
                | Send request -> { m with NextRequestId = max m.NextRequestId (next request.Id) }, acc @ [ Http request ]
                | ScheduleReconcileRead(r, ms) -> m, acc @ [ Timer(ReconcileTimer r, ms) ]
                | Announce(p, text) -> m, acc @ [ LiveAnnounce(p, text) ]
                // The conflict machine's export is filled and downloaded with the conflict review slice.
                | ExportCommands _ -> m, acc)
            (model, [])

    let private announceFault (f: SaveFault) = LiveAnnounce(Assertive, "That action is not possible now: " + f.Reason)

    // ---- SM-LOAD ------------------------------------------------------------------------------------------------------

    /// SM-LOAD OpenRequested (or RefreshRequested from Loaded) for a listed or saved investigation.
    let openRef (now: DateTimeOffset) (r: InvestigationRef) (model: Model) : Model * HostEffect list =
        match contextFor now model (Some r) with
        | None -> model, []
        | Some ctx ->
            let current = model.Loads |> Map.tryFind r |> Option.defaultValue NotLoaded
            let result =
                match current with
                | Loaded _ -> saveMachine.StepLoad ctx current LoadInput.RefreshRequested
                | Loading _ -> Ok(current, [])
                | _ -> Persistence.openInvestigation ctx (online model) current r
            match result with
            | Ok(state, effects) -> effectsOf { model with Loads = Map.add r state model.Loads } effects
            | Error _ -> model, []

    /// The route's investigation is opened when the engine knows where it is and does not hold it yet.
    let openRoute (now: DateTimeOffset) (model: Model) : Model * HostEffect list =
        match Ui.UiRoutes.parse (Routes.hashOf model.Route) with
        | Causa.Ui.Contracts.WorkspaceRoute(t, _) ->
            match Working.refOf model t with
            | Some r when not (model.Sessions.ContainsKey r && not (Working.pending (Map.tryFind r model.Sessions)).IsEmpty) ->
                match Map.tryFind r model.Loads with
                | None
                | Some NotLoaded
                | Some(LoadFailed _) -> openRef now r model
                | _ -> model, []
            | _ -> model, []
        | _ -> model, []

    /// How many working-copy investigations are opened at once to name them in the list. Every listed investigation
    /// is shown (by its identity until its title is read); titles are read a few at a time, never all at once.
    [<Literal>]
    let MaxOpening = 6

    /// Opens the next listed working-copy investigations not read yet, while fewer than MaxOpening are being read.
    let openMore (now: DateTimeOffset) (model: Model) : Model * HostEffect list =
        let loading = model.Loads |> Map.filter (fun _ l -> match l with Loading _ -> true | _ -> false) |> Map.count
        let waiting =
            model.Listings
            |> Map.toList
            |> List.filter (fun (a, _) -> a.Role = WorkspaceRole.WorkingCopy)
            |> List.collect (fun (_, l) -> l.Investigations |> List.map (fun i -> i.Ref))
            |> List.filter (fun r -> not (model.Loads.ContainsKey r))
            |> List.truncate (max 0 (MaxOpening - loading))
        waiting |> List.fold (fun (m, acc) r -> let m, e = openRef now r m in m, acc @ e) (model, [])

    let private loadStep (now: DateTimeOffset) (r: InvestigationRef) (input: LoadInput) (model: Model) =
        match contextFor now model (Some r), Map.tryFind r model.Loads with
        | Some ctx, Some state ->
            match saveMachine.StepLoad ctx state input with
            | Ok(state', effects) ->
                let model = { model with Loads = Map.add r state' model.Loads }
                // T-L03: a verified working copy gets its save session (a catalog investigation gets none); a session
                // this tab already holds keeps its unsaved changes and base.
                let model =
                    match state' with
                    | Loaded v when not (model.Sessions.ContainsKey r) ->
                        match Persistence.sessionOf v with
                        | Some s -> { model with Sessions = Map.add r s model.Sessions }
                        | None -> model
                    | _ -> model
                effectsOf model effects
            | Error _ -> model, []
        | _ -> model, []

    // ---- SM-SAVE ------------------------------------------------------------------------------------------------------

    let rec private saveStep (now: DateTimeOffset) (r: InvestigationRef) (input: SaveInput) (model: Model) : Model * HostEffect list =
        match contextFor now model (Some r), Map.tryFind r model.Sessions with
        | Some ctx, Some session ->
            let wasSaved = match session.State with SaveState.Saved _ -> true | _ -> false
            match saveMachine.Step ctx session input with
            | Ok(session', effects) ->
                let model1, hostEffects = effectsOf { model with Sessions = Map.add r session' model.Sessions } effects
                // MutationSent in the same update that sends the mutation (IF41-SAVE, T-S61).
                let model2, more =
                    match session'.Outstanding, effects with
                    | Some(id, MutationAnswer), sent when sent |> List.exists (function Send q -> q.Id = id | _ -> false) ->
                        saveStep now r (SaveInput.MutationSent id) model1
                    | _ -> model1, []
                // A confirmed save: read the investigation back as GitHub now holds it, so the verified version the
                // views show is the saved one (SM-LOAD OpenRequested, or RefreshRequested from Loaded: T-L07).
                let model3, reload =
                    match session'.State with
                    | SaveState.Saved _ when not wasSaved -> openRef now r model2
                    | _ when session'.BaseHead <> session.BaseHead -> openRef now r model2
                    | _ -> model2, []
                model3, hostEffects @ more @ reload
            | Error fault -> model, [ announceFault fault ]
        | _ -> model, []

    /// An edit: SM-SAVE Edit, then WentOffline when the browser is offline (the machine has no network flag).
    let private edit (now: DateTimeOffset) (r: InvestigationRef) (command: CommandEnvelope) (model: Model) =
        let model, effects = saveStep now r (SaveInput.Edit command) model
        match model.Network, Map.tryFind r model.Sessions with
        | NetworkState.Offline, Some s when s.State = SaveState.Unsaved ->
            let model, more = saveStep now r SaveInput.WentOffline model
            model, effects @ more
        | _ -> model, effects

    /// A domain command is decided by the kernel against the investigation's current state before it becomes an
    /// edit (UI-004: the engine decides legality). A refused command changes nothing and says why.
    let private command (now: DateTimeOffset) (r: InvestigationRef) (env: CommandEnvelope) (model: Model) =
        match Kernel.domain.Decide Policy.core Working.actor (Working.current model r) env with
        | Allowed _ -> edit now r env model
        | Blocked blockers -> model, [ LiveAnnounce(Assertive, blockers |> List.map (fun b -> b.Message) |> String.concat " ") ]
        | RequiresReview reasons
        | UnsupportedSemantics reasons -> model, [ LiveAnnounce(Assertive, String.concat " " reasons) ]
        | Conflict _ -> model, [ LiveAnnounce(Assertive, "The investigation changed; the action was not applied.") ]
        | Duplicate _ -> model, []

    // ---- SM-CONFLICT --------------------------------------------------------------------------------------------------

    /// The re-decision of this tab's unsaved commands on GitHub's version (T-C07, PERSIST-012): each in order, at the
    /// version the previous ones reached; a command the kernel refuses is kept aside with the kernel's reason.
    let reapply (remote: VerifiedInvestigation) (commands: CommandEnvelope list) : CommandEnvelope list * RefusedCommand list =
        commands
        |> List.fold
            (fun (state: Investigation option, accepted, refused) (c: CommandEnvelope) ->
                let c = { c with ExpectedVersion = state |> Option.map (fun s -> s.Version) }
                match Kernel.domain.Decide Policy.core Working.actor state c with
                | Allowed _ -> Working.apply remote.Investigation.UpdatedAt state c, accepted @ [ c ], refused
                | Blocked blockers -> state, accepted, refused @ [ { Command = c; Reason = blockers |> List.map (fun b -> b.Message) |> String.concat " " } ]
                | RequiresReview reasons
                | UnsupportedSemantics reasons -> state, accepted, refused @ [ { Command = c; Reason = String.concat " " reasons } ]
                | Conflict _ -> state, accepted, refused @ [ { Command = c; Reason = "decided at another version" } ]
                | Duplicate _ -> state, accepted, refused @ [ { Command = c; Reason = "already applied on GitHub" } ])
            (Some remote.Investigation, [], [])
        |> fun (_, accepted, refused) -> accepted, refused

    /// The export of a session's unsaved commands (one JSON object per command, the CommandEnvelope's fields).
    let exportOf (session: InvestigationSession) : string * string =
        let (InvestigationId g) = session.Id
        let lines =
            Working.pending (Some session)
            |> List.map (fun c ->
                let (InvestigationId inv) = c.Investigation
                let kind =
                    match c.Command with
                    | Core core -> (sprintf "%A" core).Split([| ' '; '\n' |], 2).[0]
                    | Extension(id, _) -> string id
                System.Text.Json.JsonSerializer.Serialize(
                    dict [ "commandId", box (c.CommandId.ToString("D"))
                           "investigationId", box (inv.ToString("D"))
                           "expectedVersion", box (c.ExpectedVersion |> Option.map (fun (AggregateVersion v) -> string v) |> Option.defaultValue "")
                           "command", box kind
                           "detail", box (sprintf "%A" c.Command) ]
                ))
        sprintf "causa-unsaved-%s.json" (g.ToString("D")), "[\n" + String.concat ",\n" lines + "\n]\n"

    let rec private conflictStep (now: DateTimeOffset) (r: InvestigationRef) (input: ConflictInput) (model: Model) =
        match contextFor now model (Some r), Map.tryFind r model.Sessions with
        | Some ctx, Some({ State = SaveState.Conflict view } as session) ->
            match saveMachine.StepConflict ctx view input with
            | Ok(view', effects) ->
                // The machine asks for the export; the engine fills it with the session's unsaved commands.
                let effects =
                    effects |> List.filter (function ExportCommands _ -> false | _ -> true)
                let model, hostEffects =
                    effectsOf { model with Sessions = Map.add r { session with State = SaveState.Conflict view' } model.Sessions } effects
                match input, view' with
                // Reapplying: the engine re-decides and reports ReapplyDone.
                | ConflictInput.ReapplyChosen, Reapplying remote ->
                    let accepted, refused = reapply remote (Working.pending (Some session))
                    let model, more = conflictStep now r (ConflictInput.ReapplyDone(accepted, refused)) model
                    model, hostEffects @ more
                | ConflictInput.ReapplyDone(accepted, _), Reapplied _ ->
                    let model, more = saveStep now r (SaveInput.ConflictResolvedReapplied accepted) model
                    model, hostEffects @ more
                | ConflictInput.DiscardConfirmed, Discarded remote ->
                    let model, more = saveStep now r (SaveInput.ConflictResolvedDiscarded remote) model
                    model, hostEffects @ more
                | _ -> model, hostEffects
            | Error fault -> model, [ announceFault fault ]
        | _ -> model, []

    /// Export of the unsaved commands during a conflict: a download, and in Unresolvable also ExportChosen.
    let private export (now: DateTimeOffset) (r: InvestigationRef) (model: Model) =
        match Map.tryFind r model.Sessions with
        | Some({ State = SaveState.Conflict view } as session) ->
            let download = Download(exportOf session)
            match view with
            | Unresolvable _ ->
                let model, effects = conflictStep now r ConflictInput.ExportChosen model
                model, download :: effects
            | _ -> model, [ download ]
        | _ -> model, []

    // ---- listing ------------------------------------------------------------------------------------------------------

    let private list (now: DateTimeOffset) (target: ListingTarget) (model: Model) =
        match contextFor now model None with
        | Some ctx ->
            let step, request = Persistence.lister.List ctx target
            { model with
                NextRequestId = next request.Id
                ListingsInFlight = Map.add request.Id (target, step) model.ListingsInFlight },
            [ Http request ]
        | None -> model, []

    /// After Connect: the active catalog and this application's working copy (EXT-DATA-003, EXT-DATA-009).
    let private listAll (now: DateTimeOffset) (model: Model) =
        let model, a = list now ActiveCatalog model
        match WorkspaceAddress.workingCopy [] WorkingCopyRun with
        | Some address ->
            let model, b = list now (WorkingCopyOf address) model
            model, a @ b
        | None -> model, a

    let private listingAnswer (now: DateTimeOffset) (target, step) (outcome: HttpOutcome) (model: Model) =
        let id = idOf outcome
        let model = { model with ListingsInFlight = Map.remove id model.ListingsInFlight }
        match contextFor now model None with
        | None -> model, []
        | Some ctx ->
            match Persistence.lister.InterpretList ctx target step outcome with
            | ListingNextRequest(step', request) ->
                { model with
                    NextRequestId = next request.Id
                    ListingsInFlight = Map.add request.Id (target, step') model.ListingsInFlight },
                [ Http request ]
            | ListingComplete listing ->
                let model = { model with Listings = Map.add listing.Address listing model.Listings }
                // A working copy lists ids only: they are opened a few at a time so the list can name them by their
                // titles (and they are ready to edit); then the route's investigation, if the listing made it known.
                let model, opened =
                    match listing.Address.Role with
                    | WorkspaceRole.WorkingCopy -> openMore now model
                    | WorkspaceRole.Catalog -> model, []
                let model, routeOpen = openRoute now model
                model, opened @ routeOpen
            | ListingFailed failure ->
                let what =
                    match target with
                    | ActiveCatalog -> "the active catalog"
                    | WorkingCopyOf _ -> "this application's working copy"
                let why =
                    match failure with
                    | Unverifiable _ -> "its files did not verify"
                    | ProviderFailed e -> sprintf "GitHub answered %A" e
                model, [ LiveAnnounce(Assertive, sprintf "Could not list %s: %s." what why) ]

    // ---- SM-CONNECTION ------------------------------------------------------------------------------------------------

    let private connection (now: DateTimeOffset) (input: ConnectionInput) (model: Model) : Model * HostEffect list =
        match ConnectionMachine.step model.NextRequestId model.Connection input with
        | Error f -> model, [ LiveAnnounce(Assertive, "That is not possible now (" + ConnectionMachine.faultId f + ").") ]
        | Ok state ->
            match input with
            | ConnectionInput.ConnectSubmitted(repository, credential) ->
                let context = { Repository = repository; Credential = credential }
                let request = Persistence.connector.ConnectRequest model.NextRequestId context
                { model with Connection = state; Context = Some context; NextRequestId = next request.Id }, [ Http request ]
            | ConnectionInput.ReconnectSubmitted credential ->
                match model.Context with
                | Some c ->
                    let context = { c with Credential = credential }
                    let request = Persistence.connector.ConnectRequest model.NextRequestId context
                    { model with Connection = state; Context = Some context; NextRequestId = next request.Id }, [ Http request ]
                | None -> model, []
            | ConnectionInput.ConnectSucceeded _ ->
                let model = { model with Connection = state }
                // The first connect lists the repository; a reconnect keeps everything already in the tab (T-N06).
                if model.Listings.IsEmpty && model.ListingsInFlight.IsEmpty then listAll now model else model, []
            | ConnectionInput.ConnectRefused e ->
                let cause =
                    match e with
                    | AuthenticationFailed -> "GitHub did not accept the token."
                    | PermissionDenied _ -> "The token cannot read this repository."
                    | NotFound _ -> "GitHub found no such repository, or the token cannot see it."
                    | RateLimited _ -> "GitHub's rate limit was reached. Try again later."
                    | NetworkUnavailable _ -> "GitHub could not be reached."
                    | _ -> "GitHub refused the connection."
                // T-N03 leaves Disconnected: the credential is dropped with the context.
                let context = if state = Disconnected then None else model.Context
                { model with Connection = state; Context = context }, [ LiveAnnounce(Assertive, "Not connected. " + cause) ]
            // T-N08, T-N09: the credential and the tab's state are dropped.
            | ConnectionInput.DisconnectConfirmed -> { initialModel model.Route with NextRequestId = model.NextRequestId; Network = model.Network }, []
            | ConnectionInput.AuthFailed -> { model with Connection = state }, []

    let private connectAnswer (now: DateTimeOffset) (outcome: HttpOutcome) (model: Model) =
        match Persistence.connector.InterpretConnect (idOf outcome) outcome with
        | ConnectAccepted canWrite -> connection now (ConnectionInput.ConnectSucceeded canWrite) model
        | ConnectRejected e -> connection now (ConnectionInput.ConnectRefused e) model
        | ConnectSuperseded -> model, []

    // ---- SM-NETWORK ---------------------------------------------------------------------------------------------------

    let private network (now: DateTimeOffset) (input: NetworkInput) (model: Model) =
        match NetworkMachine.step model.Network input with
        | Error _ -> model, []
        | Ok state ->
            let model = { model with Network = state }
            let saveInput, loadInput =
                match input with
                | NetworkInput.OfflineObserved -> SaveInput.WentOffline, LoadInput.WentOffline
                | NetworkInput.OnlineObserved -> SaveInput.WentOnline, LoadInput.WentOnline
            // T-W01 / T-W02: every session and load hears it; a pair its machine does not take changes nothing.
            let model, saves =
                model.Sessions
                |> Map.toList
                |> List.fold
                    (fun (m, acc) (r, s) ->
                        match contextFor now m (Some r) with
                        | Some ctx ->
                            match saveMachine.Step ctx s saveInput with
                            | Ok(s', effects) ->
                                let m, e = effectsOf { m with Sessions = Map.add r s' m.Sessions } effects
                                m, acc @ e
                            | Error _ -> m, acc
                        | None -> m, acc)
                    (model, [])
            let model, loads =
                model.Loads
                |> Map.toList
                |> List.fold
                    (fun (m, acc) (r, l) ->
                        match contextFor now m (Some r) with
                        | Some ctx ->
                            match saveMachine.StepLoad ctx l loadInput with
                            | Ok(l', effects) ->
                                let m, e = effectsOf { m with Loads = Map.add r l' m.Loads } effects
                                m, acc @ e
                            | Error _ -> m, acc
                        | None -> m, acc)
                    (model, [])
            let model, routeOpen = if state = NetworkState.Online then openRoute now model else model, []
            model, saves @ loads @ routeOpen

    // ---- answers ------------------------------------------------------------------------------------------------------

    let private answer (now: DateTimeOffset) (outcome: HttpOutcome) (model: Model) : Model * HostEffect list =
        let id = idOf outcome
        let sessionWaiting =
            model.Sessions
            |> Map.tryFindKey (fun _ s ->
                match s.Outstanding, s.State with
                | Some(rid, _), _ when rid = id -> true
                | _, SaveState.Conflict(ReadingRemote rid) when rid = id -> true
                | _ -> false)
        let loadWaiting =
            model.Loads
            |> Map.tryFindKey (fun _ l ->
                match l with
                | Loading(rid, _) -> rid = id
                | _ -> false)
        match model.Connection, Map.tryFind id model.ListingsInFlight, loadWaiting, sessionWaiting with
        | Connecting rid, _, _, _ when rid = id -> connectAnswer now outcome model
        | _, Some waiting, _, _ -> listingAnswer now waiting outcome model
        | _, _, Some r, _ ->
            match contextFor now model (Some r) with
            | Some ctx ->
                match saveMachine.InterpretLoad ctx model.Loads[r] outcome with
                | LoadEvent input ->
                    let model, effects = loadStep now r input model
                    let model, more = openMore now model
                    model, effects @ more
                | LoadNextRequest(state, request) ->
                    { model with Loads = Map.add r state model.Loads; NextRequestId = next request.Id }, [ Http request ]
            | None -> model, []
        | _, _, _, Some r ->
            match contextFor now model (Some r) with
            | Some ctx ->
                let session = model.Sessions[r]
                match session.State with
                | SaveState.Conflict view when session.Outstanding.IsNone || (match view with ReadingRemote rid -> rid = id | _ -> false) ->
                    match saveMachine.InterpretConflict ctx view outcome with
                    | ConflictEvent input -> conflictStep now r input model
                    | ConflictNextRequest(view', request) ->
                        { model with
                            Sessions = Map.add r { session with State = SaveState.Conflict view' } model.Sessions
                            NextRequestId = next request.Id },
                        [ Http request ]
                | _ ->
                    match saveMachine.Interpret ctx session outcome with
                    | MachineEvent input -> saveStep now r input model
                    | NextRequest(session', request) ->
                        { model with Sessions = Map.add r session' model.Sessions; NextRequestId = next request.Id }, [ Http request ]
            | None -> model, []
        // A connect answer while re-authenticating (its request is the latest connect read).
        | Reauthenticating, _, _, _ -> connectAnswer now outcome model
        | _ -> model, []

    // ---- the frozen IF41-APP step -------------------------------------------------------------------------------------

    /// One frozen IF41-APP message at the host clock `now`: the next model and the effects the host performs.
    let stepAt (now: DateTimeOffset) (msg: Msg) (model: Model) : Model * HostEffect list =
        match msg with
        | Navigate route -> openRoute now { model with Route = route }
        | Network input -> network now input model
        | Browser(ConnectivityChanged false) -> network now NetworkInput.OfflineObserved model
        | Browser(ConnectivityChanged true) -> network now NetworkInput.OnlineObserved model
        | Browser(HttpCompleted outcome) -> answer now outcome model
        | Browser(TimerFired(ReconcileTimer r)) ->
            match contextFor now model (Some r), Map.tryFind r model.Sessions with
            | Some ctx, Some s ->
                match saveMachine.ReconcileDue ctx s with
                | Ok(s', effects) -> effectsOf { model with Sessions = Map.add r s' model.Sessions } effects
                | Error _ -> model, []
            | _ -> model, []
        | Browser LeaveAttempted -> model, []
        | Connection input -> connection now input model
        | SessionInput(r, input) ->
            match input with
            | SaveInput.Edit env -> command now r env model
            // T-L07 and the machine's network flag: a save while known offline is the Offline transition instead.
            | SaveInput.SaveRequested when model.Network = NetworkState.Offline -> saveStep now r SaveInput.WentOffline model
            | _ -> saveStep now r input model
        // Export: the download of the unsaved commands (and ExportChosen in Unresolvable; no change in Reviewable).
        | ConflictStep(r, ConflictInput.ExportChosen) -> export now r model
        | ConflictStep(r, input) -> conflictStep now r input model
        | LoadStep(r, input) -> loadStep now r input model
        | ListRequested target -> list now target model
        | CreateRequested(writable, start) ->
            let address = Writable.address writable
            match Map.tryFind address model.Listings with
            | Some listing ->
                let r = { Address = address; Id = start.Investigation }
                if model.Sessions.ContainsKey r || listing.Investigations |> List.exists (fun i -> i.Ref = r) then
                    model, [ LiveAnnounce(Assertive, "An investigation with this identifier already exists.") ]
                else
                    match Kernel.domain.Decide Policy.core Working.actor None start with
                    | Allowed _ ->
                        let session = Persistence.createInvestigation writable listing.ReadAt start (online model)
                        { model with Sessions = Map.add r session model.Sessions }, []
                    | _ -> model, [ LiveAnnounce(Assertive, "The investigation could not be started.") ]
            | None -> model, [ LiveAnnounce(Assertive, "The working copy has not been listed yet.") ]
        | DomainCommand env ->
            match model.Sessions |> Map.tryFindKey (fun r _ -> r.Id = env.Investigation) with
            | Some r -> command now r env model
            | None -> model, [ LiveAnnounce(Assertive, "This investigation cannot be changed here: it is read-only.") ]

    /// The frozen IF41-APP engine value (the host clock is read when a message is stepped).
    let engine: Engine =
        { Init = fun route -> initialModel route, []
          Update = fun msg model -> stepAt DateTimeOffset.UtcNow msg model }

    // ---- the integration state ----------------------------------------------------------------------------------------

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

    let updateAt (env: HostEnv) (msg: AppMsg) (state: AppState) : AppState * AppEffect list =
        let step m model = stepAt env.Now m model
        match msg with
        | EngineMsg(Navigate route as m) ->
            let next, effects = withModel state (step m state.Model)
            let hash = Routes.hashOf route
            // A navigation the engine asked for moves the browser too (Limen pushes same-origin URLs only).
            next, effects @ [ PushLocation hash ]
        | EngineMsg m -> withModel state (step m state.Model)
        // An in-page anchor (a hash that is not a route, e.g. "#main") does not change the route.
        | LocationChanged hash when hash.Length > 1 && not (hash.StartsWith "#/") -> { state with Announcements = [] }, []
        | LocationChanged hash -> withModel state (step (Navigate(Routes.route hash)) state.Model)
        | DraftChanged(key, value) -> { state with Drafts = Map.add key value state.Drafts; Announcements = [] }, []
        | DraftsCleared keys -> { state with Drafts = keys |> List.fold (fun d k -> Map.remove k d) state.Drafts; Announcements = [] }, []
        | CredentialEntered credential -> { state with Credential = Some credential; Announcements = [] }, []
        | Announced(p, text) -> { state with Announcements = [ p, text ] }, []

    /// One message at the host's clock.
    let update (msg: AppMsg) (state: AppState) : AppState * AppEffect list =
        updateAt { Now = DateTimeOffset.UtcNow; Fresh = [] } msg state

    /// Several messages in order; their effects in order and the announcements of all of them.
    let updateAll (env: HostEnv) (msgs: AppMsg list) (state: AppState) : AppState * AppEffect list =
        msgs
        |> List.fold
            (fun (s: AppState, effects, said) m ->
                let s', e = updateAt env m s
                s', effects @ e, said @ s'.Announcements)
            (state, [], [])
        |> fun (s, effects, said) -> { s with Announcements = said }, effects
