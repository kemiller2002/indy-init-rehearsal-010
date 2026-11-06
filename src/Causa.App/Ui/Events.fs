// Owner: integration. Limen semantic events -> application messages. Screens name their events `<screen>.<action>`
// (data-event); this file is the only place an event name gains meaning.
namespace Causa.App.Ui

open Causa.Persistence
open Causa.App

/// A Limen SemanticEvent as decoded from the browser: the data-event name, the enclosing data-each row key, and the
/// element's value (inputs, selects and textareas only).
type SemanticEvent = { Name: string; Key: string option; Value: string option }

/// What the integration engine handles: the frozen IF41-APP messages plus the UI-only ones.
type AppMsg =
    /// A frozen IF41-APP message for the engine (Update.step).
    | EngineMsg of Msg
    /// The browser moved (Limen LocationChanged, Back/Forward, a followed hash link) to this hash.
    | LocationChanged of hash: string
    /// A field's text changed: kept as a draft until a command submits it.
    | DraftChanged of key: string * value: string
    /// The credential field changed: wrapped as the opaque Credential.T at once, never kept as text.
    | CredentialEntered of Credential.T
    /// These drafts are cleared (a submitted form, a cancelled start).
    | DraftsCleared of keys: string list
    /// A live-region message the engine's handling of an action produced without an engine message (a refused form).
    | Announced of Politeness * string
    /// The build record was read (its text) or could not be read (why).
    | BuildRead of Result<string, string>

/// What the integration engine asks the browser to do: the frozen IF41-APP host effects plus same-origin navigation.
type AppEffect =
    | HostEffect of HostEffect
    /// Push this hash onto the history (Limen Navigation effect).
    | PushLocation of hash: string
    /// Subscribe to the browser's connectivity (limen.lifecycle), once after Initialize.
    | SubscribeConnectivity
    /// Read the page's own build record (./build-info.json, same origin, no credential), once after Initialize.
    | ReadBuildInfo

[<RequireQualifiedAccess>]
module Events =
    /// A field whose value is a credential: its last name segment is `token` or `credential` (e.g. `connection.token`).
    /// Its value never becomes a draft (PF-015, INV-21, UI-019).
    let isSecret (name: string) =
        let last = name.Split('.') |> Array.last
        last = "token" || last = "credential"

    /// The event that navigates to the hash in its value (for controls that are not links).
    [<Literal>]
    let NavigateEvent = "app.navigate"

    /// Named actions the engine handles, by event name. Screens' actions are added here as their slices land.
    let private actions: Map<string, SemanticEvent -> AppMsg option> =
        Map.ofList
            [ NavigateEvent, (fun e -> e.Value |> Option.map (Routes.route >> Navigate >> EngineMsg)) ]

    /// The message an event means, if any. An event carrying a value is a field edit unless it is a named action;
    /// an event without a value is an action. An unknown action is ignored (the view does not change).
    let toMsg (e: SemanticEvent) : AppMsg option =
        match Map.tryFind e.Name actions with
        | Some handle -> handle e
        | None ->
            match e.Value with
            | Some v when isSecret e.Name -> Some(CredentialEntered(Credential.create v))
            | Some v -> Some(DraftChanged(Drafts.keyOf e.Name e.Key, v))
            | None -> None
