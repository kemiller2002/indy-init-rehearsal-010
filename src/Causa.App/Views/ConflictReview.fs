// Owner: ui-2. Conflict review (S23; PERSIST-011, PERSIST-012, PERSIST-031 Conflict): this tab's version, GitHub's
// version, this tab's unsaved changes (kept, never overwritten), and only the choices SM-CONFLICT offers in its current
// state: Reapply mine, Discard mine (with confirmation), Export mine, Read again. The view never resolves a conflict;
// each choice is an event integration maps to a ConflictStep (or, for Export, a download of the unsaved commands).
namespace Causa.App.Views

open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.App
open Causa.App.Ui

/// A choice the review offers, with its event name and button label.
type ConflictChoice =
    | ReapplyChoice
    | DiscardChoice
    | ConfirmDiscardChoice
    | CancelDiscardChoice
    | ExportChoice
    | ReadAgainChoice

[<RequireQualifiedAccess>]
module ConflictReview =
    /// ConflictStep(ref, ReapplyChosen).
    [<Literal>]
    let ReapplyEvent = "investigation.conflict.reapply"
    /// ConflictStep(ref, DiscardChosen): asks for confirmation (nothing is discarded yet).
    [<Literal>]
    let DiscardEvent = "investigation.conflict.discard"
    /// ConflictStep(ref, DiscardConfirmed).
    [<Literal>]
    let ConfirmDiscardEvent = "investigation.conflict.discard-confirm"
    /// ConflictStep(ref, DiscardCancelled).
    [<Literal>]
    let CancelDiscardEvent = "investigation.conflict.discard-cancel"
    /// Download of the session's unsaved commands (Unsaved @ AfterInFlight); in Unresolvable also ConflictStep ExportChosen.
    [<Literal>]
    let ExportEvent = "investigation.conflict.export"
    /// ConflictStep(ref, ReadAgain).
    [<Literal>]
    let ReadAgainEvent = "investigation.conflict.read-again"

    let eventOf (c: ConflictChoice) =
        match c with
        | ReapplyChoice -> ReapplyEvent
        | DiscardChoice -> DiscardEvent
        | ConfirmDiscardChoice -> ConfirmDiscardEvent
        | CancelDiscardChoice -> CancelDiscardEvent
        | ExportChoice -> ExportEvent
        | ReadAgainChoice -> ReadAgainEvent

    let labelOf (c: ConflictChoice) =
        match c with
        | ReapplyChoice -> "Reapply mine"
        | DiscardChoice -> "Discard mine"
        | ConfirmDiscardChoice -> "Discard my changes"
        | CancelDiscardChoice -> "Keep my changes"
        | ExportChoice -> "Export mine"
        | ReadAgainChoice -> "Read again"

    /// The choices SM-CONFLICT accepts in this state (state-machine-contracts: only legal transitions are offered).
    /// Export is PERSIST-012's third choice: it downloads this tab's changes and changes no state.
    let choices (view: ConflictView) : ConflictChoice list =
        match view with
        | Reviewable _ -> [ ReapplyChoice; DiscardChoice; ExportChoice; ReadAgainChoice ]
        | ConfirmingDiscard _ -> [ ConfirmDiscardChoice; CancelDiscardChoice ]
        | RemoteUnavailable
        | Unresolvable _ -> [ ExportChoice; ReadAgainChoice ]
        | ReadingRemote _
        | Reapplying _
        | Reapplied _
        | Discarded _ -> []

    /// What the review says in this state (never "overwrite"; this tab's changes are always kept until Discard is confirmed).
    let explanation (view: ConflictView) : string =
        match view with
        | ReadingRemote _ -> "Reading GitHub's version of this investigation. This tab's changes are kept."
        | Reviewable _ ->
            "Another writer changed this investigation on GitHub. Nothing of this tab's change was written. Reapply your changes on GitHub's version, export them, or discard them and use GitHub's version."
        | Reapplying _ -> "Reapplying this tab's changes on GitHub's version."
        | Reapplied(_, []) -> "Every change of this tab applies on GitHub's version. Save to send them."
        | Reapplied(_, refused) ->
            $"{refused.Length} of this tab's changes no longer apply on GitHub's version; the others are kept. Save to send them."
        | ConfirmingDiscard _ -> "Discard this tab's changes and use GitHub's version? This cannot be undone. Export them first to keep a copy."
        | Discarded _ -> "This tab's changes were discarded; GitHub's version is shown."
        | RemoteUnavailable -> "GitHub's version could not be read. This tab's changes are kept. Read again when the connection allows."
        | Unresolvable reason -> reason

    /// One unsaved change in words: the command and its main text.
    let describe (c: CommandEnvelope) : string =
        match c.Command with
        | Extension(id, _) -> $"Extension {id.Name.Value}"
        | Core core ->
            let detail =
                match core with
                | StartInvestigation p -> Some p.Original.Value
                | RecordObservation p -> Some p.Original.Value
                | RecordHypothesis p -> Some p.Statement.Value
                | AdmitEvidence p -> Some p.Content.Value
                | IdentifyUnknown p -> Some p.Question.Value
                | _ -> None
            let name = Commands.name core
            match detail with
            | Some d -> $"{name}: {d}"
            | None -> name

    let private versionText (i: Investigation option) (commit: CommitOid) =
        let (CommitOid oid) = commit
        let short = if oid.Length > 7 then oid.Substring(0, 7) else oid
        match i with
        | Some i -> $"Version {AggregateVersion.value i.Version} at commit {short}"
        | None -> $"Commit {short}"

    /// The frozen IF-UI ConflictPresentation of a session in Conflict (None in any other state).
    let presentation (located: LocatedInvestigation) : ConflictPresentation option =
        match located.Session with
        | Some({ State = SaveState.Conflict view } as s) ->
            let remote =
                match view with
                | Reviewable r
                | Reapplying r
                | Reapplied(r, _)
                | ConfirmingDiscard r
                | Discarded r -> Some r
                | ReadingRemote _
                | RemoteUnavailable
                | Unresolvable _ -> None
            let mine = Frame.pending (Some s)
            let tabBase = located.Verified |> Option.map (fun v -> v.Investigation)
            Some
                { ThisTabVersion =
                    $"""{versionText tabBase s.BaseHead} plus {mine.Length} change{(if mine.Length = 1 then "" else "s")} of this tab"""
                  GitHubVersion =
                    remote |> Option.map (fun r -> versionText (Some r.Investigation) r.ReadAt) |> Option.defaultValue "Not read yet"
                  UnsavedChanges = mine.Length
                  Explanation = explanation view
                  Choices = SaveIndicator.actions s.State }
        | _ -> None

    /// The review's keys. Every key is always present.
    let view (state: AppState) : (string * ViewValue) list =
        let located = Frame.locate state
        let session = located |> Option.bind (fun l -> l.Session)
        let conflictView =
            match session with
            | Some { State = SaveState.Conflict v } -> Some v
            | _ -> None
        let presentation = located |> Option.bind presentation
        let offered = conflictView |> Option.map choices |> Option.defaultValue []
        let refused =
            match conflictView with
            | Some(Reapplied(_, refused)) -> refused
            | _ -> []
        [ Bind.flag "investigation.conflict.shown" presentation.IsSome
          Bind.text "investigation.conflict.explanation" (presentation |> Option.map (fun p -> p.Explanation) |> Option.defaultValue "")
          Bind.text "investigation.conflict.mine" (presentation |> Option.map (fun p -> p.ThisTabVersion) |> Option.defaultValue "")
          Bind.text "investigation.conflict.theirs" (presentation |> Option.map (fun p -> p.GitHubVersion) |> Option.defaultValue "")
          Bind.items
              "investigation.conflict.changes"
              (Frame.pending session |> List.mapi (fun i c -> [ Cell.text "key" (string i); Cell.text "text" (describe c) ]))
          Bind.flag "investigation.conflict.hasRefused" (not refused.IsEmpty)
          Bind.items
              "investigation.conflict.refused"
              (refused |> List.mapi (fun i r -> [ Cell.text "key" (string i); Cell.text "text" (describe r.Command); Cell.text "reason" r.Reason ]))
          for c in [ ReapplyChoice; DiscardChoice; ConfirmDiscardChoice; CancelDiscardChoice; ExportChoice; ReadAgainChoice ] do
              Bind.flag (eventOf c + ".offered") (List.contains c offered) ]
