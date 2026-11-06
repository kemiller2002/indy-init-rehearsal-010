// Owner: ui-2. The save-state indicator of the investigation header (INV-015, INV-016, UI-006, PERSIST-030..032):
// exactly one of SM-SAVE's seven states, named in text inside a role=status element whose accessible name is "Save
// state", marked with the Forma status-lozenge glyph, and the actions the state offers (Save, Check again,
// Reconnect; the Conflict choices are in ConflictReview). The frozen IF41-VIEW SaveIndicatorView is the projection; the state comes only from the engine's
// session (SaveMachine), never from the view.
namespace Causa.App.Views

open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

[<RequireQualifiedAccess>]
module SaveIndicator =
    /// Save, Retry: one "Save" button (INV-015; J3-07 retries through it). Mapped by integration to SessionInput SaveRequested.
    [<Literal>]
    let SaveEvent = "investigation.save"
    /// Check again (Unknown commit outcome after the automatic reads): SessionInput CheckAgain.
    [<Literal>]
    let CheckAgainEvent = "investigation.save.check-again"
    /// Reconnect (Save failed, credential refused): Navigate to the connection region.
    [<Literal>]
    let ReconnectEvent = "investigation.save.reconnect"

    /// The seven presentation kinds, by a total match over the frozen SaveState.
    let kind (state: SaveState) : SaveStateKind =
        match state with
        | SaveState.Saved _ -> SavedKind
        | SaveState.Unsaved -> UnsavedKind
        | SaveState.Saving -> SavingKind
        | SaveState.Offline -> OfflineKind
        | SaveState.Conflict _ -> ConflictKind
        | SaveState.UnknownOutcome _ -> UnknownOutcomeKind
        | SaveState.SaveFailed _ -> SaveFailedKind

    /// The state's name (PERSIST-030: named in text; the journeys read exactly these words).
    let label (k: SaveStateKind) : string =
        match k with
        | SavedKind -> "Saved"
        | UnsavedKind -> "Unsaved"
        | SavingKind -> "Saving"
        | OfflineKind -> "Offline"
        | ConflictKind -> "Conflict"
        | UnknownOutcomeKind -> "Unknown commit outcome"
        | SaveFailedKind -> "Save failed"

    /// The Forma status-lozenge state and its glyph (the lozenge draws the glyph; never colour alone).
    let lozenge (k: SaveStateKind) : string * string =
        match k with
        | SavedKind -> "ok", "✓"
        | UnsavedKind
        | OfflineKind -> "attention", "!"
        | SavingKind
        | UnknownOutcomeKind -> "unknown", "?"
        | ConflictKind
        | SaveFailedKind -> "blocked", "×"

    let private failureCause (f: SaveFailure) =
        match f with
        | Refused e ->
            match e with
            | ValidationFailed _ -> "GitHub refused the change as invalid."
            | PermissionDenied _ -> "GitHub refused the change: this connection may not write to the repository."
            | RateLimited _ -> "GitHub refused the change because of its rate limit."
            | NotFound _ -> "GitHub refused the change: the repository or branch was not found."
            | _ -> "GitHub refused the change."
        | RepositoryKeptChanging -> "The repository kept changing on GitHub while this tab was sending the change."
        | BytesDiffer _ -> "GitHub holds a commit for this change whose files differ from what this tab wrote."
        | HistoryWasRewritten -> "The branch history was rewritten on GitHub."
        | CredentialNotAccepted -> "GitHub did not accept the credential."

    /// What the state means (PERSIST-031 words; PERSIST-032: only Saved uses the words saved, synced, stored or safe;
    /// Unsaved and Offline say the change exists only in this tab; nothing says "locally", "queued" or "later").
    let explanation (state: SaveState) : string =
        match state with
        | SaveState.Saved _ -> "All changes are saved to GitHub."
        | SaveState.Unsaved -> "Changes exist only in this tab. GitHub does not have them."
        | SaveState.Saving -> "Sending the change to GitHub. It is not confirmed yet."
        | SaveState.Offline -> "Changes exist only in this tab and cannot reach GitHub until the connection returns."
        | SaveState.Conflict _ ->
            "Another writer changed this investigation on GitHub. Nothing of this tab's change was written, and this tab's changes are kept."
        | SaveState.UnknownOutcome _ -> "The change may or may not have reached GitHub. The application is checking."
        | SaveState.SaveFailed f -> failureCause f + " Your changes are kept in this tab."

    /// The actions the state offers (PERSIST-031, PERSIST-012), each one the machine performs from that state (UI-010).
    let actions (state: SaveState) : SaveAction list =
        match state with
        | SaveState.Saved _
        | SaveState.Saving
        | SaveState.Offline -> []
        | SaveState.Unsaved -> [ Save ]
        | SaveState.Conflict(Reviewable _) -> [ ReapplyMine; DiscardMine; ExportMine ]
        | SaveState.Conflict(RemoteUnavailable)
        | SaveState.Conflict(Unresolvable _) -> [ ExportMine ]
        | SaveState.Conflict _ -> []
        // SM-SAVE refuses CheckAgain while automatic reads are left (T-S: "automatic reconciliation reads are not spent").
        | SaveState.UnknownOutcome readsLeft -> if readsLeft <= 0 then [ CheckAgainAction ] else []
        | SaveState.SaveFailed CredentialNotAccepted -> [ Reconnect ]
        | SaveState.SaveFailed _ -> [ Retry ]

    /// Conflict, Unknown commit outcome and Save failed are announced assertively; the others politely (PERSIST-030).
    let politeness (k: SaveStateKind) : Politeness =
        match k with
        | ConflictKind
        | UnknownOutcomeKind
        | SaveFailedKind -> Assertive
        | _ -> Polite

    /// The frozen IF41-VIEW projection of one session (Projections.SaveIndicator).
    let indicator (session: InvestigationSession) : SaveIndicatorView =
        let k = kind session.State
        { InvestigationId = session.Id
          Label = label k
          Text = explanation session.State
          Glyph = snd (lozenge k)
          Actions = actions session.State
          Politeness = politeness k
          LastSavedCommitTime =
            match session.State with
            | SaveState.Saved(Confirmed c) -> Some c.CommitTime
            | _ -> None }

    /// The indicator of an investigation with no save session: a verified read at a commit shows Saved (ODR-V4-08);
    /// nothing is shown while it is not loaded.
    let private ofVerified (v: VerifiedInvestigation) : SaveIndicatorView =
        { InvestigationId = v.Investigation.Id
          Label = label SavedKind
          Text = explanation (SaveState.Saved(VerifiedAt v))
          Glyph = snd (lozenge SavedKind)
          Actions = []
          Politeness = Polite
          LastSavedCommitTime = None }

    /// The route's investigation's indicator, if it has one.
    let current (state: AppState) : SaveIndicatorView option =
        Frame.locate state
        |> Option.bind (fun located ->
            match located.Session, located.Verified with
            | Some s, _ -> Some(indicator s)
            | None, Some v -> Some(ofVerified v)
            | None, None -> None)

    /// The live announcement for a session whose state kind changed (PERSIST-030-O6): the new state's name, assertive
    /// for Conflict, Unknown commit outcome and Save failed. None when the kind did not change.
    let announcement (before: InvestigationSession option) (after: InvestigationSession) : (Politeness * string) option =
        let k = kind after.State
        match before with
        | Some b when kind b.State = k -> None
        | _ -> Some(politeness k, $"Save state: {label k}")

    /// PERSIST-033: every investigation in this tab that is not Saved, with its title (as the kernel projects it) and
    /// the link to its workspace.
    let notSaved (state: AppState) : (InvestigationRef * string * string) list =
        state.Model.Sessions
        |> Map.toList
        |> List.filter (fun (_, s) -> kind s.State <> SavedKind)
        |> List.map (fun (r, s) ->
            let verified =
                match Map.tryFind r state.Model.Loads with
                | Some(Loaded v) -> Some v.Investigation
                | _ -> None
            let (InvestigationId g) = r.Id
            let title =
                Frame.working verified (Frame.pending (Some s))
                |> Option.map (fun i -> i.Title.Value)
                |> Option.defaultValue (g.ToString("D"))
            let (Workspace w) = r.Address.Workspace
            r, title, UiRoutes.href (WorkspaceRoute({ Workspace = w; Role = r.Address.Role; Id = r.Id }, Overview)))

    /// PERSIST-033, EXT-DATA-007: the connection state the workspace header shows.
    let connection (model: Model) : string =
        let repo =
            model.Context |> Option.map (fun c -> $"{c.Repository.Owner}/{c.Repository.Name} on branch {c.Repository.Branch}") |> Option.defaultValue ""
        let online = if model.Network = NetworkState.Offline then " You are offline." else ""
        match model.Connection with
        | Connected true -> $"Connected to {repo}.{online}"
        | Connected false -> $"Connected to {repo}, read-only.{online}"
        | Connecting _ -> "Connecting to the data repository…"
        | Disconnected -> "Not connected to a data repository."
        | CredentialRefused -> "GitHub did not accept the credential. Not connected."
        | Reauthenticating -> $"Reconnecting to {repo}…"

    let private stateOf (state: AppState) =
        Frame.locate state |> Option.bind (fun l -> l.Session) |> Option.map (fun s -> s.State)

    /// The indicator's keys. Every key is always present.
    let view (state: AppState) : (string * ViewValue) list =
        let shown = current state
        let k =
            match stateOf state with
            | Some s -> Some(kind s)
            | None -> shown |> Option.map (fun _ -> SavedKind)
        let has a = shown |> Option.exists (fun v -> List.contains a v.Actions)
        let unsavedHere = notSaved state
        let credentialRefused =
            match state.Model.Connection with
            | CredentialRefused
            | Reauthenticating -> true
            | _ -> false
        [ Bind.flag "investigation.save.shown" shown.IsSome
          Bind.text "investigation.save.state" (shown |> Option.map (fun v -> v.Label) |> Option.defaultValue "")
          Bind.text "investigation.save.lozenge" (k |> Option.map (lozenge >> fst) |> Option.defaultValue "unknown")
          Bind.text "investigation.save.glyph" (shown |> Option.map (fun v -> v.Glyph) |> Option.defaultValue "")
          Bind.text "investigation.save.text" (shown |> Option.map (fun v -> v.Text) |> Option.defaultValue "")
          Bind.flag "investigation.save.assertive" (shown |> Option.exists (fun v -> v.Politeness = Assertive))
          // A refused credential offers Reconnect while the connection is refused (T-N04); once a new token is accepted
          // (T-N06: "the user may save again") the same Save failed offers the save again instead.
          Bind.flag "investigation.save.canSave" (has Save || has Retry || (has Reconnect && not credentialRefused))
          Bind.flag "investigation.save.canCheckAgain" (has CheckAgainAction)
          Bind.flag "investigation.save.canReconnect" (has Reconnect && credentialRefused)
          Bind.flag "investigation.save.conflict" (k = Some ConflictKind)
          // PERSIST-030: Forma operation status for Unknown commit outcome and Save failed only.
          Bind.flag "investigation.save.operation" (k = Some UnknownOutcomeKind || k = Some SaveFailedKind)
          Bind.text "investigation.save.operationTitle" (
              match k with
              | Some UnknownOutcomeKind -> "GitHub may or may not have this change"
              | Some SaveFailedKind -> "The change did not reach GitHub"
              | _ -> "")
          // Forma freshness: the time of the last confirmed save, or that there is none in this tab.
          Bind.text "investigation.save.freshness" (
              shown |> Option.bind (fun v -> v.LastSavedCommitTime) |> Option.map Frame.formatTime |> Option.defaultValue "None in this tab")
          Bind.text "investigation.save.assertiveText" (
              shown |> Option.filter (fun v -> v.Politeness = Assertive) |> Option.map (fun v -> v.Label) |> Option.defaultValue "")
          Bind.flag "investigation.save.offlineSaved" (state.Model.Network = NetworkState.Offline && k = Some SavedKind)
          Bind.text "investigation.connection" (connection state.Model)
          Bind.flag "investigation.notSaved.any" (not unsavedHere.IsEmpty)
          Bind.text "investigation.notSaved.summary" (
              match unsavedHere.Length with
              | 0 -> ""
              | 1 -> "1 investigation in this tab has changes GitHub does not have:"
              | n -> $"{n} investigations in this tab have changes GitHub does not have:")
          Bind.items "investigation.notSaved.items" (
              unsavedHere |> List.map (fun (r, title, href) ->
                  let (InvestigationId g) = r.Id
                  let (Workspace w) = r.Address.Workspace
                  [ Cell.text "key" (w + "/" + g.ToString("D")); Cell.text "title" title; Cell.text "href" href ]))
          Bind.flag "investigation.save.hasLastSaved" (shown |> Option.exists (fun v -> v.LastSavedCommitTime.IsSome))
          Bind.text "investigation.save.lastSaved" (
              shown |> Option.bind (fun v -> v.LastSavedCommitTime) |> Option.map Frame.formatTime |> Option.defaultValue "")
          Bind.text "investigation.save.lastSavedIso" (
              shown |> Option.bind (fun v -> v.LastSavedCommitTime) |> Option.map (fun t -> t.ToUniversalTime().ToString("o")) |> Option.defaultValue "") ]
