// Owner: ui-2. The save-state indicator of the investigation header (INV-015, INV-016, UI-006, PERSIST-030..032):
// exactly one of SM-SAVE's seven states, named in text inside a role=status element whose accessible name is "Save
// state", marked with the Forma status-lozenge glyph, and the actions the state offers (Save, Check again,
// Reconnect, Review). The frozen IF41-VIEW SaveIndicatorView is the projection; the state comes only from the engine's
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
    /// Review (Conflict): ConflictStep ReviewChosen.
    [<Literal>]
    let ReviewEvent = "investigation.save.review"

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
        | Refused _ -> "GitHub refused the save."
        | RepositoryKeptChanging -> "The repository kept changing while this tab was saving."
        | BytesDiffer _ -> "GitHub holds a commit for this save whose files differ from what this tab wrote."
        | HistoryWasRewritten -> "The branch history was rewritten on GitHub."
        | CredentialNotAccepted -> "GitHub did not accept the credential."

    /// What the state means (PERSIST-031 words; the cause the machine determined, PERSIST-033).
    let explanation (state: SaveState) : string =
        match state with
        | SaveState.Saved _ -> "All changes are saved to GitHub."
        | SaveState.Unsaved -> "Changes exist only in this tab and have not been saved to GitHub."
        | SaveState.Saving -> "Saving to GitHub. The change is not confirmed yet."
        | SaveState.Offline -> "Changes exist only in this tab and cannot be saved until the connection returns."
        | SaveState.Conflict _ ->
            "Another writer changed this investigation on GitHub. Nothing of this tab's change was written, and this tab's changes are kept."
        | SaveState.UnknownOutcome _ -> "The save may or may not have reached GitHub. The application is checking."
        | SaveState.SaveFailed f -> failureCause f + " Your changes are kept in this tab."

    /// The actions the state offers (PERSIST-031).
    let actions (state: SaveState) : SaveAction list =
        match state with
        | SaveState.Saved _
        | SaveState.Saving
        | SaveState.Offline -> []
        | SaveState.Unsaved -> [ Save ]
        | SaveState.Conflict _ -> [ Review; ReapplyMine; DiscardMine; ExportMine ]
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
        [ Bind.flag "investigation.save.shown" shown.IsSome
          Bind.text "investigation.save.state" (shown |> Option.map (fun v -> v.Label) |> Option.defaultValue "")
          Bind.text "investigation.save.lozenge" (k |> Option.map (lozenge >> fst) |> Option.defaultValue "unknown")
          Bind.text "investigation.save.glyph" (shown |> Option.map (fun v -> v.Glyph) |> Option.defaultValue "")
          Bind.text "investigation.save.text" (shown |> Option.map (fun v -> v.Text) |> Option.defaultValue "")
          Bind.flag "investigation.save.assertive" (shown |> Option.exists (fun v -> v.Politeness = Assertive))
          Bind.flag "investigation.save.canSave" (has Save || has Retry)
          Bind.flag "investigation.save.canCheckAgain" (has CheckAgainAction)
          Bind.flag "investigation.save.canReconnect" (has Reconnect)
          Bind.flag "investigation.save.canReview" (has Review)
          Bind.flag "investigation.save.hasLastSaved" (shown |> Option.exists (fun v -> v.LastSavedCommitTime.IsSome))
          Bind.text "investigation.save.lastSaved" (
              shown |> Option.bind (fun v -> v.LastSavedCommitTime) |> Option.map Frame.formatTime |> Option.defaultValue "")
          Bind.text "investigation.save.lastSavedIso" (
              shown |> Option.bind (fun v -> v.LastSavedCommitTime) |> Option.map (fun t -> t.ToUniversalTime().ToString("o")) |> Option.defaultValue "") ]
