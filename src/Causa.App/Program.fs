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
        /// The browser's connectivity (limen.lifecycle: the subscription's first state, then each change).
        | Connectivity of online: bool
        /// The answer to the build record read (same origin): its text, or why it could not be read.
        | BuildInfo of Result<string, string>
        /// A limen.schedule timeout fired; its correlation id names the timer.
        | TimerDue of correlationId: string
        /// Results of effects the engine does not wait on (navigation) and messages it does not use.
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

    /// The correlation id of the build record read; never an http-<n> id, so it never reaches the GitHub machines.
    [<Literal>]
    let BuildInfoCorrelation = "build-info"

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
            | Some r when str r "kind" = Some "HttpResult" && str r "correlationId" = Some BuildInfoCorrelation ->
                match prop r "outcome" with
                | Some o when str o "kind" = Some "Success" ->
                    let status = prop o "status" |> Option.bind (fun s -> match s.TryGetInt32() with | true, v -> Some v | _ -> None)
                    match status, prop o "body" with
                    | Some 200, Some b when b.ValueKind = JsonValueKind.String -> BuildInfo(Ok(textOf b))
                    | Some 200, Some b -> BuildInfo(Ok(b.GetRawText()))
                    | s, _ -> BuildInfo(Error(sprintf "build-info.json answered %s" (s |> Option.map string |> Option.defaultValue "without a status")))
                | Some o -> BuildInfo(Error(sprintf "build-info.json could not be read (%s)" (str o "kind" |> Option.defaultValue "no outcome")))
                | None -> BuildInfo(Error "build-info.json could not be read")
            | Some r when str r "kind" = Some "HttpResult" ->
                match str r "correlationId" |> Option.bind requestIdOf, prop r "outcome" with
                | Some id, Some outcome -> HttpResult(httpOutcome id outcome)
                | _ -> Ignored
            | Some r when str r "kind" = Some "CapabilityResult" ->
                let completed = prop r "outcome" |> Option.filter (fun o -> str o "kind" = Some "Completed") |> Option.bind (fun o -> prop o "result")
                match str r "capability", completed with
                | Some "limen.lifecycle", Some result when str result "kind" = Some "Subscribed" ->
                    match prop result "state" |> Option.bind (fun st -> prop st "online") with
                    | Some v when v.ValueKind = JsonValueKind.False -> Connectivity false
                    | _ -> Ignored
                | Some "limen.schedule", Some result when str result "kind" = Some "Fired" ->
                    match str r "correlationId" with
                    | Some id -> TimerDue id
                    | None -> Ignored
                | _ -> Ignored
            | _ -> Ignored
        | Some "CapabilityFact" when str m "capability" = Some "limen.lifecycle" ->
            match prop m "fact" with
            | Some f when str f "kind" = Some "ConnectivityChanged" ->
                match prop f "online" with
                | Some v when v.ValueKind = JsonValueKind.True -> Connectivity true
                | Some v when v.ValueKind = JsonValueKind.False -> Connectivity false
                | _ -> Ignored
            | _ -> Ignored
        | _ -> Ignored

    /// The optional Limen capabilities this engine uses: connectivity (SM-NETWORK), timers (reconciliation reads) and
    /// downloads (conflict export).
    let wanted = [ "limen.lifecycle"; "limen.schedule"; "limen.files" ]

    /// The engine's handshake answer to an Initialize that carried one: the host's protocol and contract, and the
    /// offered capabilities this engine uses. None for a legacy Initialize.
    let handshakeAnswer (json: string) : (string * string list) option =
        use doc = JsonDocument.Parse json
        let m = doc.RootElement
        match str m "kind", prop m "handshake" with
        | Some "Initialize", Some h when h.ValueKind = JsonValueKind.Object ->
            match prop h "protocol", prop h "contract" with
            | Some protocol, Some contract ->
                let selected =
                    match prop h "capabilities" with
                    | Some c when c.ValueKind = JsonValueKind.Array ->
                        [ for o in c.EnumerateArray() do
                              if str o "id" |> Option.exists (fun id -> List.contains id wanted) then yield o ]
                    | _ -> []
                let raw =
                    sprintf """{"kind":"Accepted","protocol":%s,"contract":%s,"capabilities":[%s]}"""
                        (protocol.GetRawText()) (contract.GetRawText()) (selected |> List.map (fun o -> o.GetRawText()) |> String.concat ",")
                Some(raw, selected |> List.choose (fun o -> str o "id"))
            | _ -> None
        | _ -> None

    /// A timer's correlation id: the investigation it is for (workspace and id), so the answer finds its session.
    let timerId (sequence: int) (TimerKey.ReconcileTimer r) =
        let (Workspace w) = r.Address.Workspace
        let (Causa.Domain.InvestigationId g) = r.Id
        sprintf "timer|%d|%s|%s" sequence w (g.ToString("D"))

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
        | HostEffect(Timer(key, ms)) ->
            w.WriteStartObject()
            w.WriteString("kind", "Capability")
            w.WriteString("correlationId", timerId sequence key)
            w.WriteString("capability", "limen.schedule")
            w.WriteNumber("version", 1)
            w.WriteStartObject("request")
            w.WriteString("operation", "timeout")
            w.WriteNumber("delayMs", max 0 ms)
            w.WriteEndObject()
            w.WriteEndObject()
        | HostEffect(Download(fileName, content)) ->
            w.WriteStartObject()
            w.WriteString("kind", "Capability")
            w.WriteString("correlationId", "download-" + string sequence)
            w.WriteString("capability", "limen.files")
            w.WriteNumber("version", 1)
            w.WriteStartObject("request")
            w.WriteString("operation", "download")
            w.WriteString("fileName", fileName)
            w.WriteString("mimeType", "application/json")
            w.WriteString("format", "text")
            w.WriteString("data", content)
            w.WriteEndObject()
            w.WriteEndObject()
        | ReadBuildInfo ->
            w.WriteStartObject()
            w.WriteString("kind", "Http")
            w.WriteString("correlationId", BuildInfoCorrelation)
            w.WriteString("method", "GET")
            w.WriteString("url", "./build-info.json")
            w.WriteStartObject("headers")
            w.WriteEndObject()
            w.WriteNumber("timeoutMs", 10000)
            w.WriteString("response", "text")
            w.WriteString("credentials", "omit")
            w.WriteEndObject()
        | SubscribeConnectivity ->
            w.WriteStartObject()
            w.WriteString("kind", "Capability")
            w.WriteString("correlationId", "lifecycle-" + string sequence)
            w.WriteString("capability", "limen.lifecycle")
            w.WriteNumber("version", 1)
            w.WriteStartObject("request")
            w.WriteString("operation", "subscribe")
            w.WriteStartArray("topics")
            w.WriteStringValue("connectivity")
            w.WriteEndArray()
            w.WriteEndObject()
            w.WriteEndObject()
        // The leave guard needs a capability pack no slice has wired yet. LiveAnnounce is rendered (announce.*).
        | HostEffect(SetLeaveGuard _)
        | HostEffect(LiveAnnounce _) -> ()

    let private writePrimitive (w: Utf8JsonWriter) (name: string) (p: ViewPrimitive) =
        match p with
        | BindingPrimitive.Text s -> w.WriteString(name, s)
        | BindingPrimitive.Number n -> w.WriteNumber(name, n)
        | BindingPrimitive.Flag f -> w.WriteBoolean(name, f)

    /// The EngineToBrowserMessage JSON for a view and its effects.
    let encodeWith (handshake: string option) (view: (string * ViewValue) list) (effects: AppEffect list) (sequence: int) : string =
        use stream = new MemoryStream()
        do
            use w = new Utf8JsonWriter(stream)
            w.WriteStartObject()
            handshake |> Option.iter (fun h -> w.WritePropertyName "handshake"; w.WriteRawValue h)
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

    /// The EngineToBrowserMessage JSON for a view and its effects (no handshake).
    let encode (view: (string * ViewValue) list) (effects: AppEffect list) (sequence: int) : string =
        encodeWith None view effects sequence

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
            let current = UiAdapter.investigation state
            // A choice with exactly one option is that option (the view shows it selected).
            let only (keys: Guid list) = match keys with [ k ] -> Some k | _ -> None
            let evidence =
                d "investigation.link.evidence" |> guidOf
                |> Option.orElse (current |> Option.bind (fun s -> s.Evidence |> Map.toList |> List.map (fst >> fun (EvidenceId g) -> g) |> only))
                |> Option.map EvidenceId
            let hypothesis =
                d "investigation.link.hypothesis" |> guidOf
                |> Option.orElse (current |> Option.bind (fun s -> s.Hypotheses |> Map.toList |> List.map (fst >> fun (HypothesisId g) -> g) |> only))
                |> Option.map HypothesisId
            let kind = Causa.App.Workspace.Analysis.storedKindOf (d "investigation.link.kind")
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
            // T-N08, T-N09: confirmation is asked first when anything in the tab is not Saved (PERSIST-019).
            | ConnectForm.Disconnect ->
                let unsaved = state.Model.Sessions |> Map.exists (fun _ s -> match s.State with SaveState.Saved _ -> false | _ -> true)
                if unsaved && d ConnectForm.ConfirmDisconnect = "" then Some [ DraftChanged(ConnectForm.ConfirmDisconnect, "1") ]
                else Some [ DraftsCleared [ ConnectForm.ConfirmDisconnect ]; EngineMsg(Connection ConnectionInput.DisconnectConfirmed) ]
            | ConnectForm.KeepConnection -> Some [ DraftsCleared [ ConnectForm.ConfirmDisconnect ] ]
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
            | "investigation.conflict.reapply" -> Some(routeMsg state (fun r -> ConflictStep(r, ConflictInput.ReapplyChosen)))
            | "investigation.conflict.discard" -> Some(routeMsg state (fun r -> ConflictStep(r, ConflictInput.DiscardChosen)))
            | "investigation.conflict.discard-confirm" -> Some(routeMsg state (fun r -> ConflictStep(r, ConflictInput.DiscardConfirmed)))
            | "investigation.conflict.discard-cancel" -> Some(routeMsg state (fun r -> ConflictStep(r, ConflictInput.DiscardCancelled)))
            | "investigation.conflict.read-again" -> Some(routeMsg state (fun r -> ConflictStep(r, ConflictInput.ReadAgain)))
            | "investigation.conflict.export" -> Some(routeMsg state (fun r -> ConflictStep(r, ConflictInput.ExportChosen)))
            // Reconnect: the connection region (Investigations), where the token can be entered again; the session stays.
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
                // causa-storage v1.0.0 stores the evidence's content and source; the label has no field (OD-HC-06). With
                // no content entered the label is the content; with no source entered, the source is this application.
                let orElse (fallback: string) (v: string) = if System.String.IsNullOrWhiteSpace v then fallback else v
                let content = d "investigation.evidence.content" |> orElse (d "investigation.evidence.label")
                let source = d "investigation.evidence.source" |> orElse "Entered by the investigator in Causa"
                match bounded "Evidence content" 10000 content, bounded "Source" 2000 source with
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
                            [ "investigation.evidence.content"; "investigation.evidence.source"; "investigation.evidence.label"; "investigation.evidence.url" ]
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
            | BuildInfo read, Some s -> Update.updateAt env (BuildRead read) s
            | Connectivity online, Some s -> Update.updateAt env (EngineMsg(Browser(ConnectivityChanged online))) s
            | TimerDue id, Some s ->
                match id.Split('|') with
                | [| "timer"; _; workspace; guid |] ->
                    match s.Model.Sessions |> Map.tryFindKey (fun r _ -> r.Address.Workspace = Workspace workspace && r.Id = Causa.Domain.InvestigationId(Guid.Parse guid)) with
                    | Some r -> Update.updateAt env (EngineMsg(Browser(TimerFired(ReconcileTimer r)))) s
                    | None -> { s with Announcements = [] }, []
                | _ -> { s with Announcements = [] }, []
            | Ignored, Some s -> { s with Announcements = [] }, []
        let next, effects = result
        // PERSIST-030-O6: a session whose save state changed is announced (assertive for Conflict, Unknown commit
        // outcome and Save failed).
        let announced =
            match state with
            | Some before ->
                next.Model.Sessions
                |> Map.toList
                |> List.choose (fun (r, s) ->
                    let old = Map.tryFind r before.Model.Sessions
                    if old = Some s then None else Causa.App.Views.SaveIndicator.announcement old s)
            | None -> []
        seedIdentifier env { next with Announcements = next.Announcements @ announced }, effects

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
        // Initialize: answer the host's handshake, and subscribe to connectivity when the lifecycle pack is offered.
        let handshake = handshakeAnswer messageJson
        let effects =
            match handshake with
            | Some(_, selected) when List.contains "limen.lifecycle" selected -> effects @ [ SubscribeConnectivity ]
            | _ -> effects
        current <- Some(next, sequence + List.length effects)
        encodeWith (handshake |> Option.map fst) (View.render next) effects sequence
