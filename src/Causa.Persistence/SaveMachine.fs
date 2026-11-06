// Owner: persistence. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
namespace Causa.Persistence

open Causa.Domain

/// IF41-SAVE: the SM-SAVE, SM-CONFLICT and SM-LOAD machines (requirements-compiled/v4/state-machines.json). Pure;
/// effects are data. Each state union has exactly the machine's states and each input union exactly its events
/// (check X05); state-machine-contracts.json decides every (state, input) pair.
/// Confirmation (PERSIST-023): the only proof that a save succeeded. Its representation is private to this module, so
/// no other file, in this assembly or another, can build one; its only constructor requires the bytes re-read at the
/// commit to equal the bytes written. (A caller can still pass the same bytes twice: the plan's INV-01 tests guard
/// that path.)
[<RequireQualifiedAccess>]
module Confirmation =
    type T =
        private
            { Investigation: InvestigationId
              Commit: CommitOid
              CommittedAt: System.DateTimeOffset }
        member c.InvestigationId = c.Investigation
        member c.CommitOid = c.Commit
        member c.CommitTime = c.CommittedAt

    let ofReadBack (investigation: InvestigationId) (commit: CommitOid) (committedAt: System.DateTimeOffset) (written: FileSet) (readBack: FileSet) : T option =
        if written = readBack then Some { Investigation = investigation; Commit = commit; CommittedAt = committedAt } else None

type WriteMarker = { Session: System.Guid; Sequence: int64 }

type SaveFailure =
    | Refused of ProviderError
    | RepositoryKeptChanging
    | BytesDiffer of CommitOid
    | HistoryWasRewritten
    | CredentialNotAccepted

/// An unsaved command the engine refused when it was re-decided against the GitHub version, with the engine's reason
/// (T-C07, PERSIST-012).
type RefusedCommand = { Command: CommandEnvelope; Reason: string }

/// SM-CONFLICT states.
type ConflictView =
    | ReadingRemote of RequestId
    | Reviewable of remote: VerifiedInvestigation
    | Reapplying of remote: VerifiedInvestigation
    | Reapplied of remote: VerifiedInvestigation * refused: RefusedCommand list
    | ConfirmingDiscard of remote: VerifiedInvestigation
    | Discarded of remote: VerifiedInvestigation
    | RemoteUnavailable
    | Unresolvable of reason: string

/// SM-CONFLICT events.
[<RequireQualifiedAccess>]
type ConflictInput =
    | RemoteVerified of VerifiedInvestigation
    | RemoteReadFailed of TransportFailure
    | RemoteMissingOrInvalid of VerificationFailure option
    | ReadAgain
    | ReviewChosen
    | ReapplyChosen
    | ReapplyDone of accepted: CommandEnvelope list * refused: RefusedCommand list
    | DiscardChosen
    | DiscardConfirmed
    | DiscardCancelled
    | ExportChosen
    | SupersededReply of RequestId

/// What a Saved state rests on: a save confirmed by read-back (PERSIST-023), or a verified read at a named commit after
/// a load or a confirmed discard. The owner approved that the second shows as Saved (ODR-V4-08, APR-V4-OWNER-1).
type SavedBasis =
    | Confirmed of Confirmation.T
    | VerifiedAt of VerifiedInvestigation

/// SM-SAVE states (PERSIST-031: exactly seven).
[<RequireQualifiedAccess>]
type SaveState =
    | Saved of SavedBasis
    | Unsaved
    | Saving
    | Offline
    | Conflict of ConflictView
    | UnknownOutcome of automaticReadsLeft: int
    | SaveFailed of SaveFailure

/// SM-SAVE events. Persistence produces every reply event from an HttpOutcome with SaveMachine.Interpret; the App
/// produces only Edit, SaveRequested, CheckAgain, WentOffline, WentOnline, QueuedSaveStarted and MutationSent. Engine.Update
/// (IF41-APP) steps MutationSent with the RequestId in the same update that turns the step's mutation request
/// (Outstanding awaiting MutationAnswer) into HostEffect.Http. The session is unchanged, so a missing MutationSent
/// changes no outcome (T-S61, ODR-V41-08).
[<RequireQualifiedAccess>]
type SaveInput =
    | Edit of CommandEnvelope
    | SaveRequested
    | MutationSent of RequestId
    | ReadBackMatched of Confirmation.T
    | ReadBackMismatched of CommitOid
    | StaleHeadUnrelated of freshHead: CommitOid
    | StaleHeadSameInvestigation of freshHead: CommitOid
    | ProviderRefused of ProviderError
    | CredentialRefused
    | AnswerLost
    | FailedBeforeSending
    | ReconcileMarkerBytesMatch of Confirmation.T
    | ReconcileMarkerBytesDiffer of CommitOid
    | ReconcileNothingWrittenHeadUnchanged
    | ReconcileNothingWrittenHeadMoved of head: CommitOid
    | ReconcileHistoryRewritten
    | ReconcileReadFailed
    | ReconcileReadsSpent
    | CheckAgain
    | WentOffline
    | WentOnline
    | ConflictResolvedReapplied of CommandEnvelope list
    | ConflictResolvedDiscarded of VerifiedInvestigation
    | SupersededReply of RequestId
    | QueuedSaveStarted
    | RebaseReadFailed
    | ReadBackReadFailed

/// Why a load failed: the files did not verify, or the provider refused or did not answer (IF41-ERRORS), so the
/// application can state the cause truthfully (PERSIST-024).
type LoadFailure =
    | Unverifiable of VerificationFailure list
    | ProviderFailed of ProviderError

/// The read a load is waiting for.
type LoadAwaiting =
    | LoadHead
    | LoadFilesAt of CommitOid

/// SM-LOAD states.
type LoadState =
    | NotLoaded
    | Loading of RequestId * LoadAwaiting
    | Loaded of VerifiedInvestigation
    | LoadFailed of LoadFailure
    | UnavailableOffline

/// SM-LOAD events.
[<RequireQualifiedAccess>]
type LoadInput =
    | OpenRequested of InvestigationRef
    | LoadVerified of VerifiedInvestigation
    | LoadRejected of LoadFailure
    | WentOffline
    | WentOnline
    | RefreshRequested
    | SupersededReply of RequestId

type Politeness =
    | Polite
    | Assertive

type SaveEffect =
    | Send of HttpRequest
    | ScheduleReconcileRead of InvestigationRef * afterMilliseconds: int
    | Announce of Politeness * text: string
    | ExportCommands of CommandEnvelope list

/// The fail-closed answer to an illegal (state, input) pair: state, base head and unsaved changes are unchanged.
type SaveFault = { Machine: string; State: string; Input: string; Reason: string }

/// The answer a session is waiting for, so Interpret knows what it means. These are steps inside Saving and
/// UnknownOutcome; no machine state is added.
type Awaiting =
    | MutationAnswer
    | ReadBackAt of CommitOid
    | FreshHead
    | FileComparisonAt of freshHead: CommitOid
    | ReconcileHead
    | ReconcileCommits of page: int
    | ReconcileFilesAt of CommitOid

type InvestigationSession =
    { Id: InvestigationId
      /// v4.1.5 W1: where the investigation is saved. Only a working copy has a session (T-L03 as amended by ODR-V415-01).
      Address: Writable.T
      BaseHead: CommitOid
      State: SaveState
      Unsaved: CommandEnvelope list
      AfterInFlight: CommandEnvelope list
      Outstanding: (RequestId * Awaiting) option
      Mutations: int
      RebaseAttempts: int
      ReconcileReads: int
      /// v4.1.2 (ODR-V412-01): when each reconciliation read of this session was sent, automatic or not, newest first.
      /// One entry per reconciliation read: the first request of a reconciliation; the commit pages and file reads that
      /// complete the same reconciliation are not further reads. Every operation that sends a reconciliation read
      /// (ReconcileDue, T-S31, T-S32) sends it only while fewer than four entries fall in the 60 s before
      /// RequestContext.Now (INV-04), and records it here. Never cleared; older entries count for nothing.
      ReconcileReadsSentAt: System.DateTimeOffset list }

/// What one answer means for a session.
type Interpretation =
    /// The answer completes a step: apply this event with SaveMachine.Step.
    | MachineEvent of SaveInput
    /// The answer leads to the next read of the same step (read-back after the mutation, file comparison after a
    /// fresh head, the next reconciliation page): send the request; the session, whose Outstanding now names it, keeps
    /// its state.
    | NextRequest of InvestigationSession * HttpRequest

/// What one answer means for a load.
type LoadInterpretation =
    | LoadEvent of LoadInput
    | LoadNextRequest of LoadState * HttpRequest

/// What one answer means for a conflict review.
type ConflictInterpretation =
    | ConflictEvent of ConflictInput
    | ConflictNextRequest of ConflictView * HttpRequest

/// v4.1.5 L1: one investigation a workspace holds. For a catalog, the title and expected state digest come from the
/// catalog manifest (EXT-DATA-013); for a working copy they are absent until the investigation is opened.
type ListedInvestigation = { Ref: InvestigationRef; Path: RepoPath; Title: string option; ExpectedStateSha256: string option }

/// v4.1.5 L1, W1: what to list. ActiveCatalog derives the catalog workspace from `catalog/active.json`; a working copy
/// comes only from WorkspaceAddress.workingCopy.
type ListingTarget =
    | ActiveCatalog
    | WorkingCopyOf of WorkspaceAddress.T

/// v4.1.5 L1: the read a listing is waiting for, so InterpretList knows what an answer means and at which commit.
type ListingStep =
    | ListHead
    | ListActiveCatalogAt of CommitOid
    | ListManifestAt of CommitOid * Workspace
    | ListDirectoryAt of CommitOid * Workspace

/// v4.1.5 L1: a workspace's investigations, all read at one commit.
type Listing = { Address: WorkspaceAddress.T; ReadAt: CommitOid; Investigations: ListedInvestigation list }

/// v4.1.5 L1: what one answer means for a listing. A listing is a read, like a load's head-then-files steps; it adds
/// no machine state.
type ListingInterpretation =
    | ListingComplete of Listing
    | ListingNextRequest of ListingStep * HttpRequest
    | ListingFailed of LoadFailure

/// v4.1.5 L1, W1: listing a workspace. Nothing is written (EXT-DATA-006). A working copy that does not exist yet lists
/// as empty at the head read (EXT-DATA-009: a clean start).
type WorkspaceLister =
    { List: RequestContext -> ListingTarget -> ListingStep * HttpRequest
      InterpretList: RequestContext -> ListingTarget -> ListingStep -> HttpOutcome -> ListingInterpretation }

/// Every operation that emits a request uses RequestContext.NextId for it (at most one per call) and records it as
/// Outstanding; an answer whose RequestId is not Outstanding is SupersededReply.
type SaveMachine =
    { Initial: Writable.T -> VerifiedInvestigation -> InvestigationSession
      /// v4.1.5 C1 (address, id, base head, start command, online): a new investigation's session in a working copy at the head just read, holding its
      /// StartInvestigation command (EXT-DATA-008), the only way it starts. It starts in Unsaved when online and in
      /// Offline when known offline (the states T-S01 and T-S02 would reach). Its first save is the existing T-S04: one
      /// commit adding StorageCodec.EncodeNew's three files, conditional on BaseHead, Saved only from an equal read-back
      /// (PERSIST-005..007). On a moved head, files absent at the fresh head are StaleHeadUnrelated and files present
      /// there are StaleHeadSameInvestigation. No state, input or transition is added.
      InitialNew: Writable.T -> InvestigationId -> CommitOid -> CommandEnvelope -> bool -> InvestigationSession
      /// Classifies and verifies one answer (IF41-ERRORS, read-back, reconciliation) against what the session awaits.
      Interpret: RequestContext -> InvestigationSession -> HttpOutcome -> Interpretation
      Step: RequestContext -> InvestigationSession -> SaveInput -> Result<InvestigationSession * SaveEffect list, SaveFault>
      /// The timer of ScheduleReconcileRead fired: in UnknownOutcome with automatic reads left, send the next
      /// reconciliation read (state unchanged); with none left, Step ReconcileReadsSpent. v4.1.2 (ODR-V412-01): the
      /// read is sent, and recorded in ReconcileReadsSentAt, only while fewer than four reconciliation reads were sent
      /// in the 60 s before RequestContext.Now; otherwise ScheduleReconcileRead fires again 60 s after the oldest of
      /// those four, and nothing is sent.
      ReconcileDue: RequestContext -> InvestigationSession -> Result<InvestigationSession * SaveEffect list, SaveFault>
      InterpretConflict: RequestContext -> ConflictView -> HttpOutcome -> ConflictInterpretation
      StepConflict: RequestContext -> ConflictView -> ConflictInput -> Result<ConflictView * SaveEffect list, SaveFault>
      InterpretLoad: RequestContext -> LoadState -> HttpOutcome -> LoadInterpretation
      StepLoad: RequestContext -> LoadState -> LoadInput -> Result<LoadState * SaveEffect list, SaveFault> }

/// One investigation's files as one GraphQL read returned them: the commit they were read at and each file's bytes
/// (None when the commit does not hold it).
type InvestigationFilesAnswer =
    { Commit: CommitOid
      CommittedAt: System.DateTimeOffset
      Manifest: byte[] option
      Events: byte[] option
      Snapshot: byte[] option }

[<RequireQualifiedAccess>]
module InvestigationFiles =
    open System.Text.Json

    let private prop (name: string) (e: JsonElement) =
        if e.ValueKind = JsonValueKind.Object then
            match e.TryGetProperty name with
            | true, v when v.ValueKind <> JsonValueKind.Null -> Some v
            | _ -> None
        else None

    let private blob (commit: JsonElement) (alias: string) : Result<byte[] option, string> =
        match prop alias commit |> Option.bind (prop "object") with
        | None -> Ok None
        | Some b ->
            match prop "isTruncated" b, prop "text" b with
            | Some t, _ when t.ValueKind = JsonValueKind.True -> Error(sprintf "%s is too large to read in one answer" alias)
            | _, Some text ->
                match text.GetString() with
                | null -> Error "binary file"
                | s -> Ok(Some(System.Text.Encoding.UTF8.GetBytes s))
            | _, None -> Error "binary file"

    /// Decodes a classified answer to GitHubRequests.investigationFiles.
    let decode (answer: ProviderAnswer) : Result<InvestigationFilesAnswer, ProviderError> =
        try
            use doc = JsonDocument.Parse answer.Body
            match prop "data" doc.RootElement |> Option.bind (prop "repository") |> Option.bind (prop "object") with
            | None -> Error(NotFound "revision")
            | Some commit ->
                let oid = prop "oid" commit |> Option.bind (fun o -> Option.ofObj (o.GetString()))
                let at =
                    prop "committedDate" commit
                    |> Option.bind (fun d -> Option.ofObj (d.GetString()))
                    |> Option.bind (fun d ->
                        match System.DateTimeOffset.TryParse(d, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal) with
                        | true, v -> Some v
                        | _ -> None)
                match oid, blob commit "m", blob commit "e", blob commit "s" with
                | Some o, Ok m, Ok e, Ok s ->
                    Ok { Commit = CommitOid o; CommittedAt = at |> Option.defaultValue System.DateTimeOffset.MinValue; Manifest = m; Events = e; Snapshot = s }
                | None, _, _, _ -> Error(Malformed "the files answer has no commit oid")
                | _, Error d, _, _
                | _, _, Error d, _
                | _, _, _, Error d -> Error(Malformed d)
        with _ -> Error(Malformed "the files answer does not decode")

    /// The complete file set, when all three files are present.
    let fileSet (a: InvestigationFilesAnswer) : FileSet option =
        match a.Manifest, a.Events, a.Snapshot with
        | Some m, Some e, Some s -> Some { Manifest = m; Events = e; Snapshot = s }
        | _ -> None

    /// Whether the commit holds none of the investigation's files (a new investigation not yet written).
    let absent (a: InvestigationFilesAnswer) = a.Manifest.IsNone && a.Events.IsNone && a.Snapshot.IsNone

/// A commit a save sends: the request, the exact bytes it writes, and the version it produces once confirmed.
type PreparedSave =
    { Commit: CommitRequest
      Written: FileSet
      History: EventEnvelope list
      Investigation: Investigation
      /// The write marker trailer value (PERSIST-014).
      Marker: string }

/// What the machine needs from the domain and the storage layout.
type SaveSupport =
    { Kernel: DomainKernel
      Policies: PolicySet
      Actor: ActorContext
      /// The actor every event of this tab records (CONTRACT section 5.3).
      EventActor: Actor
      /// `<workspace>/investigations/<id>` (StorageCodec.PathOf).
      PathOf: Workspace -> InvestigationId -> RepoPath }

/// SM-SAVE, SM-CONFLICT and SM-LOAD as state-machine-contracts.json decides them (IF41-SAVE).
[<RequireQualifiedAccess>]
module SaveMachines =
    open System
    open System.Collections.Generic

    let private saveStateName (s: SaveState) =
        match s with
        | SaveState.Saved _ -> "Saved"
        | SaveState.Unsaved -> "Unsaved"
        | SaveState.Saving -> "Saving"
        | SaveState.Offline -> "Offline"
        | SaveState.Conflict _ -> "Conflict"
        | SaveState.UnknownOutcome _ -> "UnknownOutcome"
        | SaveState.SaveFailed _ -> "SaveFailed"

    let private inputName (i: SaveInput) =
        match i with
        | SaveInput.Edit _ -> "Edit"
        | SaveInput.SaveRequested -> "SaveRequested"
        | SaveInput.MutationSent _ -> "MutationSent"
        | SaveInput.ReadBackMatched _ -> "ReadBackMatched"
        | SaveInput.ReadBackMismatched _ -> "ReadBackMismatched"
        | SaveInput.StaleHeadUnrelated _ -> "StaleHeadUnrelated"
        | SaveInput.StaleHeadSameInvestigation _ -> "StaleHeadSameInvestigation"
        | SaveInput.ProviderRefused _ -> "ProviderRefused"
        | SaveInput.CredentialRefused -> "CredentialRefused"
        | SaveInput.AnswerLost -> "AnswerLost"
        | SaveInput.FailedBeforeSending -> "FailedBeforeSending"
        | SaveInput.ReconcileMarkerBytesMatch _ -> "ReconcileMarkerBytesMatch"
        | SaveInput.ReconcileMarkerBytesDiffer _ -> "ReconcileMarkerBytesDiffer"
        | SaveInput.ReconcileNothingWrittenHeadUnchanged -> "ReconcileNothingWrittenHeadUnchanged"
        | SaveInput.ReconcileNothingWrittenHeadMoved _ -> "ReconcileNothingWrittenHeadMoved"
        | SaveInput.ReconcileHistoryRewritten -> "ReconcileHistoryRewritten"
        | SaveInput.ReconcileReadFailed -> "ReconcileReadFailed"
        | SaveInput.ReconcileReadsSpent -> "ReconcileReadsSpent"
        | SaveInput.CheckAgain -> "CheckAgain"
        | SaveInput.WentOffline -> "WentOffline"
        | SaveInput.WentOnline -> "WentOnline"
        | SaveInput.ConflictResolvedReapplied _ -> "ConflictResolvedReapplied"
        | SaveInput.ConflictResolvedDiscarded _ -> "ConflictResolvedDiscarded"
        | SaveInput.SupersededReply _ -> "SupersededReply"
        | SaveInput.QueuedSaveStarted -> "QueuedSaveStarted"
        | SaveInput.RebaseReadFailed -> "RebaseReadFailed"
        | SaveInput.ReadBackReadFailed -> "ReadBackReadFailed"

    let private viewName (v: ConflictView) =
        match v with
        | ReadingRemote _ -> "ReadingRemote"
        | Reviewable _ -> "Reviewable"
        | Reapplying _ -> "Reapplying"
        | Reapplied _ -> "Reapplied"
        | ConfirmingDiscard _ -> "ConfirmingDiscard"
        | Discarded _ -> "Discarded"
        | RemoteUnavailable -> "RemoteUnavailable"
        | Unresolvable _ -> "Unresolvable"

    let private conflictInputName (i: ConflictInput) =
        match i with
        | ConflictInput.RemoteVerified _ -> "RemoteVerified"
        | ConflictInput.RemoteReadFailed _ -> "RemoteReadFailed"
        | ConflictInput.RemoteMissingOrInvalid _ -> "RemoteMissingOrInvalid"
        | ConflictInput.ReadAgain -> "ReadAgain"
        | ConflictInput.ReviewChosen -> "ReviewChosen"
        | ConflictInput.ReapplyChosen -> "ReapplyChosen"
        | ConflictInput.ReapplyDone _ -> "ReapplyDone"
        | ConflictInput.DiscardChosen -> "DiscardChosen"
        | ConflictInput.DiscardConfirmed -> "DiscardConfirmed"
        | ConflictInput.DiscardCancelled -> "DiscardCancelled"
        | ConflictInput.ExportChosen -> "ExportChosen"
        | ConflictInput.SupersededReply _ -> "SupersededReply"

    let private loadStateName (s: LoadState) =
        match s with
        | NotLoaded -> "NotLoaded"
        | Loading _ -> "Loading"
        | Loaded _ -> "Loaded"
        | LoadFailed _ -> "LoadFailed"
        | UnavailableOffline -> "UnavailableOffline"

    let private loadInputName (i: LoadInput) =
        match i with
        | LoadInput.OpenRequested _ -> "OpenRequested"
        | LoadInput.LoadVerified _ -> "LoadVerified"
        | LoadInput.LoadRejected _ -> "LoadRejected"
        | LoadInput.WentOffline -> "WentOffline"
        | LoadInput.WentOnline -> "WentOnline"
        | LoadInput.RefreshRequested -> "RefreshRequested"
        | LoadInput.SupersededReply _ -> "SupersededReply"

    let private fault machine state input reason : SaveFault =
        { Machine = machine; State = state; Input = input; Reason = reason }

    /// SM-SAVE limits (PERSIST-010, PERSIST-016; github-error-matrix.json bounds).
    [<Literal>]
    let MaxMutations = 4

    [<Literal>]
    let MaxRebases = 3

    [<Literal>]
    let AutomaticReads = 3

    [<Literal>]
    let ReadsPerMinute = 4

    let private truncateToMs (t: DateTimeOffset) =
        DateTimeOffset(t.UtcTicks - t.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero)

    /// Builds a machine. The machine keeps, in tab memory only (PERSIST-002), the verified versions sessions rest on
    /// (content-addressed by investigation and commit, so an entry never changes) and the commit each investigation
    /// last prepared (by investigation; replaced by each new prepare); every transition stays a function of the
    /// session, the input and these immutable versions.
    let create (support: SaveSupport) : SaveMachine =
        let tabSession = Guid.NewGuid()
        let gate = obj ()
        let versions = Dictionary<InvestigationId * CommitOid, VerifiedInvestigation>()
        let prepared = Dictionary<InvestigationId, PreparedSave>()
        let remember (v: VerifiedInvestigation) =
            lock gate (fun () -> versions[(v.Investigation.Id, v.ReadAt)] <- v)
        let rememberAs (id: InvestigationId) (head: CommitOid) (v: VerifiedInvestigation) =
            lock gate (fun () -> versions[(id, head)] <- { v with ReadAt = head })
        let versionAt (id: InvestigationId) (head: CommitOid) =
            lock gate (fun () ->
                match versions.TryGetValue((id, head)) with
                | true, v -> Some v
                | _ -> None)
        let lastPrepared (id: InvestigationId) =
            lock gate (fun () ->
                match prepared.TryGetValue id with
                | true, p -> Some p
                | _ -> None)

        let refOf (s: InvestigationSession) : InvestigationRef = { Address = Writable.address s.Address; Id = s.Id }
        let dirOf (s: InvestigationSession) = support.PathOf (Writable.address s.Address).Workspace s.Id

        /// The events of the unsaved commands, decided in order on the base version (or from nothing for a new
        /// investigation), each with its section 5 provenance.
        let decideAll (ctx: RequestContext) (baseState: Investigation option) (commands: CommandEnvelope list) =
            let at = truncateToMs ctx.Now
            commands
            |> List.fold
                (fun acc (command: CommandEnvelope) ->
                    acc
                    |> Result.bind (fun (state: Investigation option, events: EventEnvelope list) ->
                        match support.Kernel.Decide support.Policies support.Actor state command with
                        | Allowed payloads ->
                            let start = state |> Option.map (fun s -> s.Version) |> Option.defaultValue (AggregateVersion 0L)
                            let (AggregateVersion v0) = start
                            let (InvestigationId inv) = command.Investigation
                            let added =
                                payloads
                                |> List.mapi (fun i payload ->
                                    let version = v0 + int64 (i + 1)
                                    { EventId = EventId(Canonical.eventId inv version command.CommandId)
                                      Investigation = command.Investigation
                                      Version = AggregateVersion version
                                      Provenance =
                                        { OccurredAt = at
                                          Actor = support.EventActor
                                          CorrelationId = command.CommandId
                                          FormatVersion = Events.formatVersion
                                          SemanticVersion = Events.semanticVersion }
                                      Payload = payload })
                            added
                            |> List.fold
                                (fun st e -> st |> Result.bind (fun s -> support.Kernel.Evolve s e |> Result.map Some |> Result.mapError (fun p -> p.Reason)))
                                (Ok state)
                            |> Result.map (fun next -> next, events @ added)
                        | Blocked blockers -> Error(blockers |> List.map (fun b -> b.Message) |> String.concat "; ")
                        | RequiresReview reasons
                        | UnsupportedSemantics reasons -> Error(String.concat "; " reasons)
                        | Conflict _ -> Error "the command was decided at another version"
                        | Duplicate _ -> Error "the command was already applied"))
                (Ok(baseState, []))

        /// The commit of a session's unsaved commands at its base head (PERSIST-005, PERSIST-006): one
        /// createCommitOnBranch with the events append, manifest and snapshot under the investigation's own prefix.
        let prepare (ctx: RequestContext) (s: InvestigationSession) : Result<PreparedSave, string> =
            let dir = dirOf s
            let (RepoPath d) = dir
            let (RequestId sequence) = ctx.NextId
            let marker = sprintf "%s.%d" (tabSession.ToString("N")) sequence
            let baseVersion = versionAt s.Id s.BaseHead
            let files =
                match baseVersion with
                | Some v ->
                    decideAll ctx (Some v.Investigation) s.Unsaved
                    |> Result.bind (fun (after, added) ->
                        match after with
                        | None -> Error "nothing to save"
                        | Some inv ->
                            FileSets.append v.Bytes v.History added
                            |> Result.mapError (fun (WireError e) -> e)
                            |> Result.map (fun fs -> fs, v.History @ added, inv))
                | None ->
                    decideAll ctx None s.Unsaved
                    |> Result.bind (fun (after, added) ->
                        match after with
                        | None -> Error "a new investigation needs its StartInvestigation command"
                        | Some inv ->
                            FileSets.ofHistory added |> Result.mapError (fun (WireError e) -> e) |> Result.map (fun fs -> fs, added, inv))
            files
            |> Result.bind (fun (written, history, inv) ->
                let commit =
                    { ExpectedHead = s.BaseHead
                      Headline = sprintf "Causa: save investigation %s" (let (InvestigationId g) = s.Id in g.ToString())
                      Body = ""
                      Marker = Some marker
                      Additions =
                        [ { Path = RepoPath(d + "/events.ndjson"); Contents = written.Events }
                          { Path = RepoPath(d + "/manifest.json"); Contents = written.Manifest }
                          { Path = RepoPath(d + "/snapshot.json"); Contents = written.Snapshot } ]
                      Deletions = [] }
                GitHubRequests.checkPrefix dir commit
                |> Result.mapError (sprintf "%A")
                |> Result.map (fun c -> { Commit = c; Written = written; History = history; Investigation = inv; Marker = marker }))

        let ok s effects : Result<InvestigationSession * SaveEffect list, SaveFault> = Ok(s, effects)

        /// Sends a prepared save (or the last prepared one, retargeted) at `expected`.
        let send (ctx: RequestContext) (s: InvestigationSession) (expected: CommitOid) (reuse: bool) =
            let p =
                match (if reuse then lastPrepared s.Id else None) with
                | Some p when p.History.Length > 0 ->
                    let (RequestId sequence) = ctx.NextId
                    let marker = sprintf "%s.%d" (tabSession.ToString("N")) sequence
                    Ok { p with Commit = { p.Commit with ExpectedHead = expected; Marker = Some marker }; Marker = marker }
                | _ -> prepare ctx { s with BaseHead = expected }
            p
            |> Result.map (fun p ->
                lock gate (fun () -> prepared[s.Id] <- p)
                let request = GitHubRequests.commitOnBranch ctx.NextId ctx.Connection p.Commit
                { s with BaseHead = expected; Outstanding = Some(ctx.NextId, MutationAnswer); Mutations = s.Mutations + 1 }, [ Send request ])

        let work (s: InvestigationSession) = s.Unsaved @ s.AfterInFlight
        let keep (s: InvestigationSession) = { s with Unsaved = work s; AfterInFlight = []; Outstanding = None }

        let schedule (s: InvestigationSession) ms = ScheduleReconcileRead(refOf s, ms)

        let recentReads (ctx: RequestContext) (s: InvestigationSession) =
            s.ReconcileReadsSentAt |> List.filter (fun t -> t > ctx.Now.AddSeconds(-60.0))

        /// One reconciliation read: the commits after the expected head (PERSIST-015).
        let reconcileRead (ctx: RequestContext) (s: InvestigationSession) =
            let request = GitHubRequests.compare ctx.NextId ctx.Connection s.BaseHead 1
            { s with Outstanding = Some(ctx.NextId, ReconcileCommits 1); ReconcileReadsSentAt = ctx.Now :: s.ReconcileReadsSentAt }, [ Send request ]

        let conflictRead (ctx: RequestContext) (s: InvestigationSession) (revision: GitHubRequests.Revision) =
            GitHubRequests.investigationFiles ctx.NextId ctx.Connection revision (dirOf s)

        let confirm (s: InvestigationSession) (c: Confirmation.T) =
            // The confirmed version becomes the base of the next save.
            match lastPrepared s.Id with
            | Some p when p.Written.Events.Length > 0 ->
                remember
                    { Investigation = p.Investigation
                      History = p.History
                      ReadAt = c.CommitOid
                      Address = Writable.address s.Address
                      Bytes = p.Written }
            | _ -> ()
            let later = s.AfterInFlight
            let basis = { s with BaseHead = c.CommitOid; Outstanding = None; AfterInFlight = []; RebaseAttempts = 0; Mutations = 0 }
            if later.IsEmpty then { basis with State = SaveState.Saved(Confirmed c); Unsaved = [] }
            else { basis with State = SaveState.Unsaved; Unsaved = later }

        let awaiting (s: InvestigationSession) =
            s.Outstanding |> Option.map snd

        let step (ctx: RequestContext) (s: InvestigationSession) (input: SaveInput) : Result<InvestigationSession * SaveEffect list, SaveFault> =
            let state = saveStateName s.State
            let name = inputName input
            let refuse reason = Error(fault "SM-SAVE" state name reason)
            let illegal () = refuse (sprintf "SM-SAVE/%s/%s is not a transition of the machine" state name)
            let startSave (s: InvestigationSession) =
                if ctx.MutationInFlightElsewhere then
                    ok { s with State = SaveState.Unsaved } [ Announce(Polite, "Waiting for another save in this tab to finish.") ]
                else
                    let fresh = { s with Mutations = 0; RebaseAttempts = 0; ReconcileReads = 0; State = SaveState.Saving; AfterInFlight = [] }
                    match send ctx { fresh with Unsaved = work s } s.BaseHead false with
                    | Ok(next, effects) -> ok next effects
                    | Error reason -> refuse ("the commit could not be built: " + reason)
            match s.State, input with
            // Saved
            | SaveState.Saved _, SaveInput.Edit c -> ok { s with State = SaveState.Unsaved; Unsaved = s.Unsaved @ [ c ] } []
            | SaveState.Saved _, (SaveInput.WentOffline | SaveInput.WentOnline) -> ok s []
            // Unsaved
            | SaveState.Unsaved, SaveInput.Edit c -> ok { s with Unsaved = s.Unsaved @ [ c ] } []
            | SaveState.Unsaved, SaveInput.SaveRequested
            | SaveState.Unsaved, SaveInput.QueuedSaveStarted -> startSave s
            | SaveState.Unsaved, SaveInput.WentOffline -> ok { keep s with State = SaveState.Offline } []
            | SaveState.Unsaved, SaveInput.WentOnline -> ok s []
            // Saving
            | SaveState.Saving, SaveInput.Edit c -> ok { s with AfterInFlight = s.AfterInFlight @ [ c ] } []
            | SaveState.Saving, SaveInput.MutationSent id ->
                match s.Outstanding with
                | Some(current, MutationAnswer) when current = id -> ok s []
                | _ -> refuse "MutationSent names no outstanding mutation"
            | SaveState.Saving, SaveInput.ReadBackMatched c ->
                match awaiting s with
                | Some(ReadBackAt commit) when commit = c.CommitOid && c.InvestigationId = s.Id -> ok (confirm s c) []
                | _ -> refuse "the confirmation does not answer the outstanding read-back"
            | SaveState.Saving, SaveInput.ReadBackMismatched commit ->
                match awaiting s with
                | Some(ReadBackAt awaited) when awaited = commit -> ok { keep s with State = SaveState.SaveFailed(BytesDiffer commit) } []
                | _ -> refuse "not the outstanding read-back"
            | SaveState.Saving, SaveInput.StaleHeadUnrelated fresh ->
                match awaiting s with
                | Some(FileComparisonAt f) when f = fresh ->
                    if s.RebaseAttempts >= MaxRebases || s.Mutations >= MaxMutations then
                        ok { keep s with State = SaveState.SaveFailed RepositoryKeptChanging } []
                    else
                        // The investigation's files are unchanged at the fresh head: the same version rests on it.
                        versionAt s.Id s.BaseHead |> Option.iter (rememberAs s.Id fresh)
                        match send ctx { s with RebaseAttempts = s.RebaseAttempts + 1 } fresh true with
                        | Ok(next, effects) -> ok next effects
                        | Error reason -> refuse ("the commit could not be rebuilt: " + reason)
                | _ -> refuse "not the outstanding file comparison"
            | SaveState.Saving, SaveInput.StaleHeadSameInvestigation fresh
            | SaveState.UnknownOutcome _, SaveInput.StaleHeadSameInvestigation fresh ->
                match awaiting s with
                | Some(FileComparisonAt f) when f = fresh ->
                    let request = conflictRead ctx s (GitHubRequests.AtCommit fresh)
                    ok { keep s with State = SaveState.Conflict(ReadingRemote ctx.NextId) } [ Send request ]
                | _ -> refuse "not the outstanding file comparison"
            | SaveState.Saving, SaveInput.ProviderRefused e ->
                match awaiting s with
                | Some MutationAnswer -> ok { keep s with State = SaveState.SaveFailed(Refused e) } []
                | _ -> refuse "not the outstanding mutation"
            | SaveState.Saving, SaveInput.CredentialRefused ->
                match awaiting s with
                | Some _ -> ok { keep s with State = SaveState.SaveFailed CredentialNotAccepted } []
                | None -> refuse "nothing outstanding"
            | SaveState.Saving, SaveInput.AnswerLost ->
                match awaiting s with
                | Some MutationAnswer ->
                    ok { s with State = SaveState.UnknownOutcome AutomaticReads; Outstanding = None; ReconcileReads = 0 } [ schedule s 2000 ]
                | _ -> refuse "not the outstanding mutation"
            | SaveState.Saving, SaveInput.FailedBeforeSending ->
                match awaiting s with
                | Some _ -> ok { keep s with State = SaveState.Offline } []
                | None -> refuse "nothing outstanding"
            | SaveState.Saving, (SaveInput.WentOffline | SaveInput.WentOnline) -> ok s []
            | SaveState.Saving, SaveInput.SupersededReply id
            | SaveState.UnknownOutcome _, SaveInput.SupersededReply id ->
                match s.Outstanding with
                | Some(current, _) when current = id -> refuse "the reply is current"
                | _ -> ok s []
            | SaveState.Saving, SaveInput.RebaseReadFailed ->
                match awaiting s with
                | Some FreshHead
                | Some(FileComparisonAt _) ->
                    ok { s with State = SaveState.UnknownOutcome AutomaticReads; Outstanding = None; ReconcileReads = 0 } [ schedule s 2000 ]
                | _ -> refuse "not the outstanding rebase read"
            | SaveState.Saving, SaveInput.ReadBackReadFailed ->
                match awaiting s with
                | Some(ReadBackAt _) ->
                    ok { s with State = SaveState.UnknownOutcome AutomaticReads; Outstanding = None; ReconcileReads = 0 } [ schedule s 2000 ]
                | _ -> refuse "not the outstanding read-back"
            // Offline
            | SaveState.Offline, SaveInput.Edit c -> ok { s with Unsaved = s.Unsaved @ [ c ] } []
            | SaveState.Offline, (SaveInput.SaveRequested | SaveInput.WentOffline | SaveInput.QueuedSaveStarted) -> ok s []
            | SaveState.Offline, SaveInput.WentOnline -> ok { s with State = SaveState.Unsaved } []
            // Conflict
            | SaveState.Conflict _, SaveInput.Edit c -> ok { s with Unsaved = s.Unsaved @ [ c ] } []
            | SaveState.Conflict _, SaveInput.WentOffline -> ok s []
            | SaveState.Conflict view, SaveInput.WentOnline ->
                match view with
                | RemoteUnavailable ->
                    let request = conflictRead ctx s GitHubRequests.BranchHead
                    ok { s with State = SaveState.Conflict(ReadingRemote ctx.NextId) } [ Send request ]
                | _ -> ok s []
            | SaveState.Conflict(Reapplied(remote, _)), SaveInput.ConflictResolvedReapplied accepted ->
                remember remote
                ok
                    { s with
                        State = SaveState.Unsaved
                        BaseHead = remote.ReadAt
                        Unsaved = accepted
                        AfterInFlight = []
                        Outstanding = None
                        Mutations = 0
                        RebaseAttempts = 0 }
                    []
            | SaveState.Conflict(Discarded remote), SaveInput.ConflictResolvedDiscarded verified when verified = remote ->
                remember verified
                ok
                    { s with
                        State = SaveState.Saved(VerifiedAt verified)
                        BaseHead = verified.ReadAt
                        Unsaved = []
                        AfterInFlight = []
                        Outstanding = None
                        Mutations = 0
                        RebaseAttempts = 0 }
                    []
            // UnknownOutcome
            | SaveState.UnknownOutcome _, SaveInput.Edit c -> ok { s with AfterInFlight = s.AfterInFlight @ [ c ] } []
            | SaveState.UnknownOutcome _, SaveInput.ReconcileMarkerBytesMatch c ->
                match awaiting s with
                | Some(ReconcileFilesAt commit) when commit = c.CommitOid && c.InvestigationId = s.Id -> ok (confirm s c) []
                | _ -> refuse "the confirmation does not answer the outstanding reconciliation read"
            | SaveState.UnknownOutcome _, SaveInput.ReconcileMarkerBytesDiffer commit ->
                match awaiting s with
                | Some(ReconcileFilesAt awaited) when awaited = commit -> ok { keep s with State = SaveState.SaveFailed(BytesDiffer commit) } []
                | _ -> refuse "not the outstanding reconciliation read"
            | SaveState.UnknownOutcome _, SaveInput.ReconcileNothingWrittenHeadUnchanged ->
                match awaiting s with
                | Some(ReconcileCommits _ | ReconcileHead) ->
                    if s.Mutations >= MaxMutations then
                        ok { keep s with State = SaveState.SaveFailed(Refused(NetworkUnavailable LostAfterSending)) } []
                    else
                        match send ctx { s with State = SaveState.Saving } s.BaseHead true with
                        | Ok(next, effects) -> ok next effects
                        | Error reason -> refuse ("the commit could not be rebuilt: " + reason)
                | _ -> refuse "not the outstanding reconciliation read"
            | SaveState.UnknownOutcome _, SaveInput.ReconcileNothingWrittenHeadMoved head ->
                match awaiting s with
                | Some(ReconcileCommits _ | ReconcileHead | FileComparisonAt _) ->
                    if s.RebaseAttempts >= MaxRebases || s.Mutations >= MaxMutations then
                        ok { keep s with State = SaveState.SaveFailed RepositoryKeptChanging } []
                    else
                        versionAt s.Id s.BaseHead |> Option.iter (rememberAs s.Id head)
                        match send ctx { s with State = SaveState.Saving; RebaseAttempts = s.RebaseAttempts + 1 } head true with
                        | Ok(next, effects) -> ok next effects
                        | Error reason -> refuse ("the commit could not be rebuilt: " + reason)
                | _ -> refuse "not the outstanding reconciliation read"
            | SaveState.UnknownOutcome _, SaveInput.ReconcileHistoryRewritten ->
                match awaiting s with
                | Some(ReconcileCommits _ | ReconcileHead) -> ok { keep s with State = SaveState.SaveFailed HistoryWasRewritten } []
                | _ -> refuse "not the outstanding reconciliation read"
            | SaveState.UnknownOutcome _, SaveInput.ReconcileReadFailed ->
                match awaiting s with
                | Some(ReconcileCommits _ | ReconcileHead | ReconcileFilesAt _ | FileComparisonAt _) ->
                    if s.ReconcileReads < AutomaticReads then
                        let delay = 2000 * (pown 2 s.ReconcileReads)
                        ok { s with State = SaveState.UnknownOutcome(AutomaticReads - s.ReconcileReads); Outstanding = None } [ schedule s delay ]
                    else
                        ok
                            { s with State = SaveState.UnknownOutcome 0; Outstanding = None }
                            [ Announce(Polite, "GitHub could not be read. Choose Check again to try once more.") ]
                | _ -> refuse "not the outstanding reconciliation read"
            | SaveState.UnknownOutcome _, SaveInput.ReconcileReadsSpent ->
                ok
                    { s with State = SaveState.UnknownOutcome 0; Outstanding = None }
                    [ Announce(Polite, "The save may or may not have reached GitHub. Choose Check again to read GitHub once more.") ]
            | SaveState.UnknownOutcome left, SaveInput.CheckAgain ->
                if left > 0 && s.ReconcileReads < AutomaticReads then refuse "automatic reconciliation reads are not spent"
                elif (recentReads ctx s).Length < ReadsPerMinute then
                    let next, effects = reconcileRead ctx s
                    ok next effects
                else
                    ok s [ Announce(Polite, "GitHub was read four times in the last minute; Check again can read again 60 seconds after the oldest of those reads.") ]
            | SaveState.UnknownOutcome _, SaveInput.WentOffline -> ok s []
            | SaveState.UnknownOutcome left, SaveInput.WentOnline ->
                if left > 0 && s.ReconcileReads < AutomaticReads then ok s [ schedule s 0 ]
                elif (recentReads ctx s).Length < ReadsPerMinute then
                    let next, effects = reconcileRead ctx s
                    ok next effects
                else ok s []
            // SaveFailed
            | SaveState.SaveFailed _, SaveInput.Edit c -> ok { s with Unsaved = s.Unsaved @ [ c ] } []
            | SaveState.SaveFailed _, SaveInput.SaveRequested -> startSave s
            | SaveState.SaveFailed _, (SaveInput.WentOffline | SaveInput.WentOnline) -> ok s []
            | _ -> illegal ()

        let reconcileDue (ctx: RequestContext) (s: InvestigationSession) =
            match s.State with
            | SaveState.UnknownOutcome _ when s.ReconcileReads < AutomaticReads && s.Outstanding.IsNone ->
                let recent = recentReads ctx s
                if recent.Length < ReadsPerMinute then
                    let next, effects = reconcileRead ctx s
                    let reads = s.ReconcileReads + 1
                    Ok({ next with ReconcileReads = reads; State = SaveState.UnknownOutcome(AutomaticReads - reads) }, effects)
                else
                    let oldest = recent |> List.min
                    let wait = int (max 0.0 ((oldest.AddSeconds 60.0 - ctx.Now).TotalMilliseconds))
                    Ok(s, [ schedule s wait ])
            | SaveState.UnknownOutcome _ when s.Outstanding.IsNone -> step ctx s SaveInput.ReconcileReadsSpent
            | _ -> Error(fault "SM-SAVE" (saveStateName s.State) "ReconcileDue" "no automatic reconciliation read is due")

        let superseded (s: InvestigationSession) (outcome: HttpOutcome) =
            let id =
                match outcome with
                | Answered(id, _, _, _) -> id
                | NotAnswered(id, _) -> id
            match s.Outstanding with
            | Some(current, awaited) when current = id -> Some awaited
            | _ -> None
            |> function
                | Some awaited -> Ok awaited
                | None -> Error(MachineEvent(SaveInput.SupersededReply id))

        let filesAt (ctx: RequestContext) (s: InvestigationSession) (revision: GitHubRequests.Revision) (awaiting: Awaiting) =
            NextRequest({ s with Outstanding = Some(ctx.NextId, awaiting) }, conflictRead ctx s revision)

        /// Compares the files a read returned with the version this session's save rests on.
        let unchangedSinceBase (s: InvestigationSession) (files: InvestigationFilesAnswer) =
            match versionAt s.Id s.BaseHead with
            | Some v -> InvestigationFiles.fileSet files = Some v.Bytes
            | None -> InvestigationFiles.absent files

        let interpret (ctx: RequestContext) (s: InvestigationSession) (outcome: HttpOutcome) : Interpretation =
            match superseded s outcome with
            | Error stale -> stale
            | Ok awaited ->
                match awaited with
                | MutationAnswer ->
                    match Classify.answer (CommitOnBranch s.BaseHead) outcome with
                    | Ok answer ->
                        match Classify.committedOid answer with
                        | Some commit -> filesAt ctx s (GitHubRequests.AtCommit commit) (ReadBackAt commit)
                        | None -> MachineEvent SaveInput.AnswerLost
                    | Error(StaleHead _) ->
                        NextRequest({ s with Outstanding = Some(ctx.NextId, FreshHead) }, GitHubRequests.branchHead ctx.NextId ctx.Connection)
                    | Error(NetworkUnavailable FailedBeforeSending) -> MachineEvent SaveInput.FailedBeforeSending
                    | Error(NetworkUnavailable _ | Unrecognised _ | Malformed _) -> MachineEvent SaveInput.AnswerLost
                    | Error AuthenticationFailed -> MachineEvent SaveInput.CredentialRefused
                    | Error e -> MachineEvent(SaveInput.ProviderRefused e)
                | ReadBackAt commit ->
                    match Classify.answer (ReadInvestigationAt(let (CommitOid c) = commit in c)) outcome |> Result.bind InvestigationFiles.decode with
                    | Ok files ->
                        let written = lastPrepared s.Id |> Option.map _.Written
                        match written, InvestigationFiles.fileSet files with
                        | Some w, Some readBack ->
                            match Confirmation.ofReadBack s.Id commit files.CommittedAt w readBack with
                            | Some c -> MachineEvent(SaveInput.ReadBackMatched c)
                            | None -> MachineEvent(SaveInput.ReadBackMismatched commit)
                        | _ -> MachineEvent(SaveInput.ReadBackMismatched commit)
                    | Error AuthenticationFailed -> MachineEvent SaveInput.CredentialRefused
                    | Error _ -> MachineEvent SaveInput.ReadBackReadFailed
                | FreshHead ->
                    match Classify.answer ReadBranchHead outcome with
                    | Ok answer ->
                        match Classify.headOid answer with
                        | Some fresh -> filesAt ctx s (GitHubRequests.AtCommit fresh) (FileComparisonAt fresh)
                        | None -> MachineEvent SaveInput.RebaseReadFailed
                    | Error(NetworkUnavailable FailedBeforeSending) -> MachineEvent SaveInput.FailedBeforeSending
                    | Error AuthenticationFailed -> MachineEvent SaveInput.CredentialRefused
                    | Error _ -> MachineEvent SaveInput.RebaseReadFailed
                | FileComparisonAt fresh ->
                    let failed () =
                        match s.State with
                        | SaveState.UnknownOutcome _ -> MachineEvent SaveInput.ReconcileReadFailed
                        | _ -> MachineEvent SaveInput.RebaseReadFailed
                    match Classify.answer (ReadInvestigationAt(let (CommitOid c) = fresh in c)) outcome |> Result.bind InvestigationFiles.decode with
                    | Ok files ->
                        match s.State, unchangedSinceBase s files with
                        | SaveState.UnknownOutcome _, true -> MachineEvent(SaveInput.ReconcileNothingWrittenHeadMoved fresh)
                        | _, true -> MachineEvent(SaveInput.StaleHeadUnrelated fresh)
                        | _, false -> MachineEvent(SaveInput.StaleHeadSameInvestigation fresh)
                    | Error(NetworkUnavailable FailedBeforeSending) when s.State = SaveState.Saving -> MachineEvent SaveInput.FailedBeforeSending
                    | Error _ -> failed ()
                | ReconcileHead ->
                    match Classify.answer ReadBranchHead outcome |> Result.map Classify.headOid with
                    | Ok(Some head) when head = s.BaseHead -> MachineEvent SaveInput.ReconcileNothingWrittenHeadUnchanged
                    | Ok(Some _) ->
                        NextRequest({ s with Outstanding = Some(ctx.NextId, ReconcileCommits 1) }, GitHubRequests.compare ctx.NextId ctx.Connection s.BaseHead 1)
                    | _ -> MachineEvent SaveInput.ReconcileReadFailed
                | ReconcileCommits page ->
                    match Classify.answer (CompareFrom s.BaseHead) outcome with
                    | Error(HistoryRewritten _) -> MachineEvent SaveInput.ReconcileHistoryRewritten
                    | Error _ -> MachineEvent SaveInput.ReconcileReadFailed
                    | Ok answer ->
                        try
                            use doc = Text.Json.JsonDocument.Parse answer.Body
                            let root = doc.RootElement
                            let status = root.GetProperty("status").GetString()
                            let total = root.GetProperty("total_commits").GetInt32()
                            let commits =
                                [ for c in root.GetProperty("commits").EnumerateArray() ->
                                      string (c.GetProperty("sha").GetString()), string (c.GetProperty("commit").GetProperty("message").GetString()) ]
                            let marker = lastPrepared s.Id |> Option.map _.Marker
                            match status with
                            | "identical" -> MachineEvent SaveInput.ReconcileNothingWrittenHeadUnchanged
                            | _ ->
                                match commits |> List.tryFind (fun (_, m) -> marker.IsSome && GitHubRequests.markerOf m = marker) with
                                | Some(sha, _) -> filesAt ctx s (GitHubRequests.AtCommit(CommitOid sha)) (ReconcileFilesAt(CommitOid sha))
                                | None when page * 100 < total ->
                                    NextRequest(
                                        { s with Outstanding = Some(ctx.NextId, ReconcileCommits(page + 1)) },
                                        GitHubRequests.compare ctx.NextId ctx.Connection s.BaseHead (page + 1)
                                    )
                                | None ->
                                    match List.tryLast commits with
                                    | Some(head, _) -> filesAt ctx s (GitHubRequests.AtCommit(CommitOid head)) (FileComparisonAt(CommitOid head))
                                    | None -> MachineEvent SaveInput.ReconcileNothingWrittenHeadUnchanged
                        with _ -> MachineEvent SaveInput.ReconcileReadFailed
                | ReconcileFilesAt commit ->
                    match Classify.answer (ReadInvestigationAt(let (CommitOid c) = commit in c)) outcome |> Result.bind InvestigationFiles.decode with
                    | Ok files ->
                        match lastPrepared s.Id, InvestigationFiles.fileSet files with
                        | Some p, Some readBack ->
                            match Confirmation.ofReadBack s.Id commit files.CommittedAt p.Written readBack with
                            | Some c -> MachineEvent(SaveInput.ReconcileMarkerBytesMatch c)
                            | None -> MachineEvent(SaveInput.ReconcileMarkerBytesDiffer commit)
                        | _ -> MachineEvent(SaveInput.ReconcileMarkerBytesDiffer commit)
                    | Error _ -> MachineEvent SaveInput.ReconcileReadFailed

        // SM-CONFLICT
        let stepConflict (ctx: RequestContext) (view: ConflictView) (input: ConflictInput) =
            let refuse reason = Error(fault "SM-CONFLICT" (viewName view) (conflictInputName input) reason)
            let reread () =
                // The investigation is named by the version under review; ReadAgain re-reads at the branch's head.
                ReadingRemote ctx.NextId
            match view, input with
            | ReadingRemote _, ConflictInput.RemoteVerified v -> Ok(Reviewable v, [])
            | ReadingRemote _, ConflictInput.RemoteReadFailed _ -> Ok(RemoteUnavailable, [])
            | ReadingRemote _, ConflictInput.RemoteMissingOrInvalid cause ->
                let reason =
                    match cause with
                    | None -> "The investigation was deleted or rewritten on GitHub; your changes are kept."
                    | Some f -> sprintf "GitHub's version does not verify (%A); your changes are kept." f
                Ok(Unresolvable reason, [])
            | ReadingRemote id, ConflictInput.SupersededReply r when r <> id -> Ok(view, [])
            | Reviewable _, ConflictInput.ReadAgain -> Ok(reread (), [])
            | Reviewable _, ConflictInput.ReviewChosen -> Ok(view, [])
            | Reviewable v, ConflictInput.ReapplyChosen -> Ok(Reapplying v, [])
            | Reviewable v, ConflictInput.DiscardChosen ->
                Ok(ConfirmingDiscard v, [ Announce(Assertive, "Discard your changes and load the GitHub version? This cannot be undone.") ])
            | Reviewable _, ConflictInput.SupersededReply _ -> Ok(view, [])
            | Reapplying v, ConflictInput.ReapplyDone(_, refused) -> Ok(Reapplied(v, refused), [])
            | ConfirmingDiscard v, ConflictInput.DiscardConfirmed -> Ok(Discarded v, [])
            | ConfirmingDiscard v, ConflictInput.DiscardCancelled -> Ok(Reviewable v, [])
            | RemoteUnavailable, ConflictInput.ReadAgain -> Ok(reread (), [])
            | Unresolvable _, ConflictInput.ReadAgain -> Ok(reread (), [])
            // The engine fills the export with the session's unsaved commands (Unsaved @ AfterInFlight).
            | Unresolvable _, ConflictInput.ExportChosen -> Ok(view, [ ExportCommands [] ])
            | _ -> refuse (sprintf "SM-CONFLICT/%s/%s is not a transition of the machine" (viewName view) (conflictInputName input))

        let interpretConflict (ctx: RequestContext) (view: ConflictView) (outcome: HttpOutcome) : ConflictInterpretation =
            let id =
                match outcome with
                | Answered(id, _, _, _) -> id
                | NotAnswered(id, _) -> id
            match view with
            | ReadingRemote current when current = id ->
                match Classify.answer (ReadInvestigationAt "conflict") outcome |> Result.bind InvestigationFiles.decode with
                | Ok files when InvestigationFiles.absent files -> ConflictEvent(ConflictInput.RemoteMissingOrInvalid None)
                | Ok _ -> ConflictEvent(ConflictInput.RemoteMissingOrInvalid(Some(VerificationFailure.SchemaInvalid("investigation", "verification of GitHub's version is pending"))))
                | Error(NetworkUnavailable f) -> ConflictEvent(ConflictInput.RemoteReadFailed f)
                | Error(NotFound _) -> ConflictEvent(ConflictInput.RemoteMissingOrInvalid None)
                | Error _ -> ConflictEvent(ConflictInput.RemoteReadFailed LostAfterSending)
            | _ -> ConflictEvent(ConflictInput.SupersededReply id)

        // SM-LOAD
        let stepLoad (ctx: RequestContext) (state: LoadState) (input: LoadInput) =
            let refuse reason = Error(fault "SM-LOAD" (loadStateName state) (loadInputName input) reason)
            match state, input with
            | NotLoaded, LoadInput.OpenRequested r
            | LoadFailed _, LoadInput.OpenRequested r ->
                let request = GitHubRequests.investigationFiles ctx.NextId ctx.Connection GitHubRequests.BranchHead (support.PathOf r.Address.Workspace r.Id)
                Ok(Loading(ctx.NextId, LoadHead), [ Send request ])
            | Loading _, LoadInput.LoadVerified v ->
                remember v
                Ok(Loaded v, [])
            | Loading _, LoadInput.LoadRejected f -> Ok(LoadFailed f, [])
            | Loading(id, _), LoadInput.SupersededReply r when r <> id -> Ok(state, [])
            | Loaded _, LoadInput.WentOffline -> Ok(state, [])
            | Loaded v, LoadInput.RefreshRequested ->
                let request =
                    GitHubRequests.investigationFiles ctx.NextId ctx.Connection GitHubRequests.BranchHead (support.PathOf v.Address.Workspace v.Investigation.Id)
                Ok(Loading(ctx.NextId, LoadHead), [ Send request ])
            | Loaded _, LoadInput.SupersededReply _ -> Ok(state, [])
            | UnavailableOffline, LoadInput.WentOnline -> Ok(NotLoaded, [])
            | _ -> refuse (sprintf "SM-LOAD/%s/%s is not a transition of the machine" (loadStateName state) (loadInputName input))

        let interpretLoad (ctx: RequestContext) (state: LoadState) (outcome: HttpOutcome) : LoadInterpretation =
            let id =
                match outcome with
                | Answered(id, _, _, _) -> id
                | NotAnswered(id, _) -> id
            match state with
            | Loading(current, _) when current = id ->
                match Classify.answer (ReadInvestigationAt "load") outcome |> Result.bind InvestigationFiles.decode with
                | Ok files when InvestigationFiles.absent files -> LoadEvent(LoadInput.LoadRejected(ProviderFailed(NotFound "investigation")))
                | Ok _ ->
                    LoadEvent(LoadInput.LoadRejected(Unverifiable [ VerificationFailure.SchemaInvalid("investigation", "the storage reader is not available in this build") ]))
                | Error e -> LoadEvent(LoadInput.LoadRejected(ProviderFailed e))
            | _ -> LoadEvent(LoadInput.SupersededReply id)

        { Initial =
            fun address verified ->
                remember verified
                { Id = verified.Investigation.Id
                  Address = address
                  BaseHead = verified.ReadAt
                  State = SaveState.Saved(VerifiedAt verified)
                  Unsaved = []
                  AfterInFlight = []
                  Outstanding = None
                  Mutations = 0
                  RebaseAttempts = 0
                  ReconcileReads = 0
                  ReconcileReadsSentAt = [] }
          InitialNew =
            fun address id head start online ->
                { Id = id
                  Address = address
                  BaseHead = head
                  State = (if online then SaveState.Unsaved else SaveState.Offline)
                  Unsaved = [ start ]
                  AfterInFlight = []
                  Outstanding = None
                  Mutations = 0
                  RebaseAttempts = 0
                  ReconcileReads = 0
                  ReconcileReadsSentAt = [] }
          Interpret = interpret
          Step = step
          ReconcileDue = reconcileDue
          InterpretConflict = interpretConflict
          StepConflict = stepConflict
          InterpretLoad = interpretLoad
          StepLoad = stepLoad }

    /// The support of this application: the domain kernel, the core policy, the tab's actor and the causa-storage
    /// layout `<workspace>/investigations/<id>`.
    let support: SaveSupport =
        { Kernel = Kernel.domain
          Policies = Transitions.corePolicy
          Actor = { ActorId = "causa-web" }
          EventActor =
            { ActorId =
                match NonEmptyText.create "causa-web" with
                | Ok t -> t
                | Error e -> failwith e
              Kind = ActorKind.Human }
          PathOf = fun (Workspace w) (InvestigationId g) -> RepoPath(sprintf "%s/investigations/%s" w (g.ToString("D"))) }

/// The SM-SAVE implementation (IF41-SAVE): the one public SaveMachine value of Causa.Persistence.
[<AutoOpen>]
module SaveMachineValue =
    let saveMachine: SaveMachine = SaveMachines.create SaveMachines.support
