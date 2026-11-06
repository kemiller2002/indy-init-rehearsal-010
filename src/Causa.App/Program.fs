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

    /// One message: the next state and the effects to perform. Pure. Before Initialize there is no state; any message
    /// then starts the engine at the root route.
    let step (state: AppState option) (inbound: Inbound) : AppState * AppEffect list =
        match inbound, state with
        | Initialize hash, _ -> Update.init hash
        | _, None -> Update.init ""
        | Event e, Some s ->
            match Events.toMsg e with
            | Some msg -> Update.update msg s
            | None -> s, []
        | Located hash, Some s -> Update.update (LocationChanged hash) s
        | HttpResult outcome, Some s -> Update.update (EngineMsg(Browser(HttpCompleted outcome))) s
        | Ignored, Some s -> { s with Announcements = [] }, []

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
