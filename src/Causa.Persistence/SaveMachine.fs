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
