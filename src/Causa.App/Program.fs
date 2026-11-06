// Owner: integration. The Limen boundary: decodes BrowserToEngineMessage JSON, runs the pure engine (Update) and
// encodes EngineToBrowserMessage JSON (view first, then effects). The one mutable cell, the tab's current state, lives
// here at the host boundary; everything it calls is pure.
namespace Causa.App

open System
open System.IO
open System.Text
open System.Text.Json
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App.Ui

[<RequireQualifiedAccess>]
module Program =
    /// One decoded Limen message.
    type Inbound =
        | Initialize of hash: string
        | Event of SemanticEvent
        | Located of hash: string
        | HttpResult of HttpOutcome
        /// Results of effects the engine does not wait on (navigation) and messages it does not use (CapabilityFact).
        | Ignored

    let private textOf (v: JsonElement) : string =
        match v.GetString() with
        | null -> ""
        | s -> s

    let private str (e: JsonElement) (name: string) =
        match e.ValueKind with
        | JsonValueKind.Object ->
            match e.TryGetProperty name with
            | true, v when v.ValueKind = JsonValueKind.String -> Some(textOf v)
            | _ -> None
        | _ -> None

    let private prop (e: JsonElement) (name: string) =
        match e.ValueKind with
        | JsonValueKind.Object ->
            match e.TryGetProperty name with
            | true, v -> Some v
            | _ -> None
        | _ -> None

    let private hashOf (message: JsonElement) =
        prop message "location" |> Option.bind (fun l -> str l "hash") |> Option.defaultValue ""

    let private requestIdOf (correlationId: string) =
        match correlationId.Split('-') with
        | [| "http"; n |] ->
            match Int64.TryParse n with
            | true, id -> Some(RequestId id)
            | _ -> None
        | _ -> None

    /// A Limen Http EffectOutcome as the frozen IF41-WIRE HttpOutcome. Persistence decodes the body; this only carries it.
    let private httpOutcome (id: RequestId) (outcome: JsonElement) : HttpOutcome =
        let status = prop outcome "status" |> Option.bind (fun s -> match s.TryGetInt32() with | true, v -> Some v | _ -> None)
        match str outcome "kind" with
        | Some "Success" ->
            let headers =
                match prop outcome "headers" with
                | Some h when h.ValueKind = JsonValueKind.Object ->
                    [ for p in h.EnumerateObject() do
                          if p.Value.ValueKind = JsonValueKind.String then (p.Name, textOf p.Value) ]
                | _ -> []
            let body =
                match prop outcome "body" with
                | Some b when b.ValueKind = JsonValueKind.String -> textOf b
                | Some b when b.ValueKind <> JsonValueKind.Null && b.ValueKind <> JsonValueKind.Undefined -> b.GetRawText()
                | _ -> ""
            Answered(id, defaultArg status 0, headers, body)
        | Some "Failure" ->
            match str outcome "reason", status with
            | Some "network", _ -> NotAnswered(id, FailedBeforeSending)
            | _, Some s -> Answered(id, s, [], "")
            | _ -> NotAnswered(id, LostAfterSending)
        | Some "OutcomeUnknown" ->
            match str outcome "reason" with
            | Some "timeout-after-dispatch" -> NotAnswered(id, TimedOut)
            | _ -> NotAnswered(id, LostAfterSending)
        // Cancelled (the engine never cancels) and anything unclassified: the request may have reached the provider.
        | _ -> NotAnswered(id, LostAfterSending)

    let decode (json: string) : Inbound =
        use doc = JsonDocument.Parse json
        let m = doc.RootElement
        match str m "kind" with
        | Some "Initialize" -> Initialize(hashOf m)
        | Some "LocationChanged" -> Located(hashOf m)
        | Some "Event" ->
            match prop m "event" with
            | Some e ->
                match str e "name" with
                | Some name -> Event { Name = name; Key = str e "key"; Value = str e "value" }
                | None -> Ignored
            | None -> Ignored
        | Some "EffectResult" ->
            match prop m "result" with
            | Some r when str r "kind" = Some "HttpResult" ->
                match str r "correlationId" |> Option.bind requestIdOf, prop r "outcome" with
                | Some id, Some outcome -> HttpResult(httpOutcome id outcome)
                | _ -> Ignored
            | _ -> Ignored
        | _ -> Ignored

    let private methodName (m: HttpMethod) =
        match m with
        | Get -> "GET"
        | Post -> "POST"
        | Put -> "PUT"
        | Patch -> "PATCH"

    /// Writes one Limen effect request; None when the effect has no Limen Core equivalent.
    let private writeEffect (w: Utf8JsonWriter) (sequence: int) (effect: AppEffect) =
        match effect with
        | HostEffect(Http request) ->
            let (RequestId id) = request.Id
            w.WriteStartObject()
            w.WriteString("kind", "Http")
            w.WriteString("correlationId", "http-" + string id)
            w.WriteString("method", methodName request.Method)
            w.WriteString("url", (request.Url: string))
            w.WriteStartObject("headers")
            for (k, v) in request.Headers do
                w.WriteString((k: string), (v: string))
            w.WriteEndObject()
            request.Body |> Option.iter (fun b -> w.WriteString("body", (b: string)))
            w.WriteNumber("timeoutMs", 30000)
            w.WriteString("response", "text")
            w.WriteEndObject()
        | PushLocation hash ->
            w.WriteStartObject()
            w.WriteString("kind", "Navigation")
            w.WriteString("correlationId", "nav-" + string sequence)
            w.WriteString("operation", "push")
            w.WriteString("url", hash)
            w.WriteEndObject()
        // Timers, the leave guard and downloads need Limen capability packs; they are wired with the slices that
        // first request them (none does yet). LiveAnnounce is rendered (announce.*), never an effect.
        | HostEffect(Timer _)
        | HostEffect(SetLeaveGuard _)
        | HostEffect(Download _)
        | HostEffect(LiveAnnounce _) -> ()

    let private writePrimitive (w: Utf8JsonWriter) (name: string) (p: ViewPrimitive) =
        match p with
        | BindingPrimitive.Text s -> w.WriteString(name, s)
        | BindingPrimitive.Number n -> w.WriteNumber(name, n)
        | BindingPrimitive.Flag f -> w.WriteBoolean(name, f)

    /// The EngineToBrowserMessage JSON for a view and its effects.
    let encode (view: (string * ViewValue) list) (effects: AppEffect list) (sequence: int) : string =
        use stream = new MemoryStream()
        do
            use w = new Utf8JsonWriter(stream)
            w.WriteStartObject()
            w.WriteStartObject("view")
            for (key, value) in view do
                match value with
                | BindingValue.Text s -> w.WriteString(key, s)
                | BindingValue.Number n -> w.WriteNumber(key, n)
                | BindingValue.Flag f -> w.WriteBoolean(key, f)
                | BindingValue.Items rows ->
                    w.WriteStartArray(key)
                    for row in rows do
                        w.WriteStartObject()
                        for KeyValue(field, p) in row do
                            writePrimitive w field p
                        w.WriteEndObject()
                    w.WriteEndArray()
            w.WriteEndObject()
            w.WriteStartArray("effects")
            effects |> List.iteri (fun i e -> writeEffect w (sequence + i) e)
            w.WriteEndArray()
            w.WriteStartArray("cancellations")
            w.WriteEndArray()
            w.WriteEndObject()
        Encoding.UTF8.GetString(stream.ToArray())

    /// The actions whose meaning needs the screens' own rules (validation) or fresh identities: each becomes the
    /// messages the engine steps. Pure: the identities and the clock come from the host (HostEnv).
    module Intents =
        open Causa.Domain
        open Causa.App.Views

        let private announce text = [ Announced(Assertive, text) ]

        let private fresh (env: HostEnv) (i: int) = List.item i env.Fresh

        /// Text the catalog bounds (min 1 non-blank, max `max`).
        let private bounded (label: string) (max: int) (value: string) : Result<NonEmptyText, string> =
            if value.Length > max then Error(sprintf "%s must be at most %d characters." label max)
            else NonEmptyText.create value |> Result.mapError (fun _ -> sprintf "%s must not be empty." label)

        let private optional (max: int) (value: string) : Result<NonEmptyText option, string> =
            if String.IsNullOrWhiteSpace value then Ok None
            elif value.Length > max then Error(sprintf "The text must be at most %d characters." max)
            else NonEmptyText.create value |> Result.map Some |> Result.mapError id

        let private guidOf (text: string) =
            match Guid.TryParse text with
            | true, g -> Some g
            | _ -> None

        let startKeys = (Start.fields |> List.map snd) @ [ Start.AttemptedKey ]

        /// A command on the investigation the route shows, at its current version.
        let private onRoute (env: HostEnv) (state: AppState) (command: CoreCommand) (drafts: string list) : AppMsg list =
            match Working.routeRef state.Model state.Route with
            | Some r ->
                let version = Working.current state.Model r |> Option.map (fun i -> i.Version)
                [ EngineMsg(
                      DomainCommand
                          { CommandId = fresh env 0
                            Investigation = r.Id
                            ExpectedVersion = version
                            Command = Core command }
                  )
                  DraftsCleared drafts ]
            | None -> announce "No investigation is open."

        let private routeMsg (state: AppState) (make: InvestigationRef -> Msg) : AppMsg list =
            match Working.routeRef state.Model state.Route with
            | Some r -> [ EngineMsg(make r) ]
            | None -> []

        let private relate (env: HostEnv) (state: AppState) (reassess: bool) : AppMsg list =
            let d = Drafts.get state
            let evidence = d "investigation.link.evidence" |> guidOf |> Option.map EvidenceId
            let hypothesis = d "investigation.link.hypothesis" |> guidOf |> Option.map HypothesisId
            let kind = Causa.App.Workspace.Analysis.storedKindOf (d "investigation.link.kind")
            let current = UiAdapter.investigation state
            match evidence, hypothesis, kind, optional 10000 (d "investigation.link.rationale") with
            | None, _, _, _ -> announce "Choose the evidence to link."
            | _, None, _, _ -> announce "Choose the hypothesis to link."
            | _, _, None, _ -> announce "This relationship kind cannot be saved in this version of the data format."
            | _, _, _, Error e -> announce e
            | Some e, Some h, Some k, Ok rationale ->
                let exists = current |> Option.exists (fun s -> not (Assessments.history s e h).IsEmpty)
                if exists && not reassess then announce "This evidence is already linked to this hypothesis. Use Reassess."
                elif not exists && reassess then announce "This evidence is not linked to this hypothesis yet."
                else
                    onRoute env state
                        (RelateEvidence
                            { RelationshipId = EvidenceRelationshipId(fresh env 1)
                              EvidenceId = e
                              HypothesisId = h
                              Kind = k
                              Rationale = rationale })
                        [ "investigation.link.rationale"; "investigation.link.kind" ]

        let handle (env: HostEnv) (state: AppState) (e: SemanticEvent) : AppMsg list option =
            let d = Drafts.get state
            match e.Name with
            | ConnectForm.Connect ->
                match ConnectForm.repository (d ConnectForm.Owner) (d ConnectForm.Repository) (d ConnectForm.Branch), state.Credential with
                | None, _ -> Some(announce "Enter the repository owner and the repository.")
                | _, None -> Some(announce "Enter a token.")
                | Some repo, Some credential ->
                    match state.Model.Connection with
                    | CredentialRefused -> Some [ EngineMsg(Connection(ConnectionInput.ReconnectSubmitted credential)) ]
                    | _ -> Some [ EngineMsg(Connection(ConnectionInput.ConnectSubmitted(repo, credential))) ]
            | ConnectForm.Disconnect -> Some [ EngineMsg(Connection ConnectionInput.DisconnectConfirmed) ]
            | "new.start" ->
                let f = Start.form state
                match Start.validateForm f, Start.workingCopy state.Model |> Option.bind Writable.ofAddress, guidOf f.Identifier with
                | _ :: _, _, _
                | _, _, None -> Some [ DraftChanged(Start.AttemptedKey, "1") ]
                | [], None, _ -> Some(announce "The working copy for new investigations has not been listed yet.")
                | [], Some w, Some g ->
                    match NonEmptyText.create (Start.titleOrFallback f), NonEmptyText.create f.ProblemStatement with
                    | Ok title, Ok original ->
                        let id = InvestigationId g
                        let start =
                            { CommandId = fresh env 0
                              Investigation = id
                              ExpectedVersion = None
                              Command = Core(StartInvestigation { Title = title; ObservationId = ObservationId(fresh env 1); Original = original }) }
                        // The primary question (INV-023) is stored as the investigation's first Unknown, id = the
                        // investigation's id: causa-storage v1.0.0 has no question field.
                        let question =
                            match Start.question f |> Option.map NonEmptyText.create with
                            | Some(Ok q) ->
                                [ EngineMsg(
                                      DomainCommand
                                          { CommandId = fresh env 2
                                            Investigation = id
                                            ExpectedVersion = Some(AggregateVersion 1L)
                                            Command = Core(IdentifyUnknown { UnknownId = UnknownId g; Question = q; Materiality = Materiality.Informational }) }
                                  ) ]
                            | _ -> []
                        let target = { Workspace = (let (Workspace ws) = (Writable.address w).Workspace in ws); Role = WorkspaceRole.WorkingCopy; Id = id }
                        Some(
                            [ EngineMsg(CreateRequested(w, start)) ]
                            @ question
                            @ [ DraftsCleared startKeys
                                EngineMsg(Navigate(Routes.route (UiRoutes.href (WorkspaceRoute(target, Overview))))) ]
                        )
                    | _ -> Some [ DraftChanged(Start.AttemptedKey, "1") ]
            | "new.cancel" -> Some [ DraftsCleared startKeys; EngineMsg(Navigate(Routes.route "#/investigations")) ]
            | "investigation.save" -> Some(routeMsg state (fun r -> SessionInput(r, SaveInput.SaveRequested)))
            | "investigation.save.check-again" -> Some(routeMsg state (fun r -> SessionInput(r, SaveInput.CheckAgain)))
            | "investigation.save.review" -> Some(routeMsg state (fun r -> ConflictStep(r, ConflictInput.ReviewChosen)))
            | "investigation.save.reconnect" -> Some [ EngineMsg(Navigate(Routes.route "#/investigations")) ]
            | "investigation.observation.record" ->
                match bounded "Observation text" 10000 (d "investigation.observation.text") with
                | Ok text ->
                    Some(onRoute env state (RecordObservation { ObservationId = ObservationId(fresh env 1); Original = text; ObservedAt = None }) [ "investigation.observation.text" ])
                | Error m -> Some(announce m)
            | "investigation.hypothesis.record" ->
                match bounded "Hypothesis statement" 10000 (d "investigation.hypothesis.statement"), optional 10000 (d "investigation.hypothesis.explanation") with
                | Ok statement, Ok mechanism ->
                    Some(
                        onRoute env state
                            (RecordHypothesis { HypothesisId = HypothesisId(fresh env 1); Statement = statement; Mechanism = mechanism })
                            [ "investigation.hypothesis.statement"; "investigation.hypothesis.explanation" ]
                    )
                | Error m, _
                | _, Error m -> Some(announce m)
            | "investigation.evidence.record" ->
                match bounded "Evidence content" 10000 (d "investigation.evidence.content"), bounded "Source" 2000 (d "investigation.evidence.source") with
                | Ok content, Ok source ->
                    Some(
                        onRoute env state
                            (AdmitEvidence
                                { EvidenceId = EvidenceId(fresh env 1)
                                  ContentKind = ContentKind.Text
                                  Content = content
                                  // causa-storage v1.0.0 requires a category and the form has none: the domain's first case.
                                  Category = EvidenceCategory.DirectObservation
                                  Source = source
                                  CapturedAt = None })
                            [ "investigation.evidence.content"; "investigation.evidence.source" ]
                    )
                | Error m, _
                | _, Error m -> Some(announce m)
            | "investigation.link.relate" -> Some(relate env state false)
            | "investigation.link.reassess" -> Some(relate env state true)
            | _ -> None

    /// Applies messages in order. A DraftsCleared that follows a domain command or a start the engine refused (the
    /// sessions did not change) is skipped, so refused input stays in its field.
    let private applyAll (env: HostEnv) (msgs: AppMsg list) (state: AppState) : AppState * AppEffect list =
        msgs
        |> List.fold
            (fun (s: AppState, effects, said, refused) m ->
                match m with
                | DraftsCleared _ when refused -> s, effects, said, refused
                | _ ->
                    let s', e = Update.updateAt env m s
                    let refused =
                        match m with
                        | EngineMsg(DomainCommand _)
                        | EngineMsg(CreateRequested _) -> refused || obj.ReferenceEquals(s'.Model.Sessions, s.Model.Sessions) || s'.Model.Sessions = s.Model.Sessions
                        | _ -> refused
                    s', effects @ e, said @ s'.Announcements, refused)
            (state, [], [], false)
        |> fun (s, effects, said, _) -> { s with Announcements = said }, effects

    /// The Start screen's identifier is drawn once per visit (the view only binds it).
    let private seedIdentifier (env: HostEnv) (state: AppState) : AppState =
        match state.Route with
        | StartRoute when not (state.Drafts.ContainsKey Causa.App.Views.Start.IdentifierEvent) ->
            { state with Drafts = Map.add Causa.App.Views.Start.IdentifierEvent ((List.last env.Fresh).ToString("D")) state.Drafts }
        | _ -> state

    /// One message: the next state and the effects to perform. Pure: the clock and fresh identities are in `env`.
    /// Before Initialize there is no state; any message then starts the engine at the root route.
    let stepWith (env: HostEnv) (state: AppState option) (inbound: Inbound) : AppState * AppEffect list =
        let result =
            match inbound, state with
            | Initialize hash, _ -> Update.init hash
            | _, None -> Update.init ""
            | Event e, Some s ->
                match Intents.handle env s e with
                | Some msgs -> applyAll env msgs s
                | None ->
                    match Events.toMsg e with
                    | Some msg -> Update.updateAt env msg s
                    | None -> { s with Announcements = [] }, []
            | Located hash, Some s -> Update.updateAt env (LocationChanged hash) s
            | HttpResult outcome, Some s -> Update.updateAt env (EngineMsg(Browser(HttpCompleted outcome))) s
            | Ignored, Some s -> { s with Announcements = [] }, []
        let next, effects = result
        seedIdentifier env next, effects

    /// One message at the host clock with fresh identities drawn here.
    let step (state: AppState option) (inbound: Inbound) : AppState * AppEffect list =
        stepWith { Now = DateTimeOffset.UtcNow; Fresh = List.init 4 (fun _ -> Guid.NewGuid()) } state inbound

    /// The tab's state and the next effect sequence number: the only mutable cell, at the host boundary.
    let mutable private current: (AppState * int) option = None

    /// The [JSExport] entry point (Causa.Host): one BrowserToEngineMessage JSON in, one EngineToBrowserMessage JSON out.
    let dispatch (messageJson: string) : string =
        let state, sequence =
            match current with
            | Some(s, n) -> Some s, n
            | None -> None, 0
        let next, effects = step state (decode messageJson)
        current <- Some(next, sequence + List.length effects)
        encode (View.render next) effects sequence
