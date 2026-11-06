// Owner: domain. The SM-SAVE / SM-CONFLICT fixture the invariant tests share, and INV-08.
namespace Causa.Invariants

open System
open System.Reflection
open Causa.Domain
open Causa.Persistence

/// Builders for domain values and save sessions. Deterministic: fixed ids, fixed instants, no randomness.
module SaveFixture =
    let guid (n: int) = Guid(sprintf "00000000-0000-4000-8000-%012d" n)

    let text (s: string) =
        match NonEmptyText.create s with
        | Ok t -> t
        | Error e -> failwith e

    let now = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)
    let investigationId = InvestigationId(guid 1)
    let actor: Actor = { ActorId = text "tester"; Kind = ActorKind.Human }

    let envelope (version: int64) (correlation: Guid) (event: CoreEvent) : EventEnvelope =
        { EventId = EventId(guid (1000 + int version))
          Investigation = investigationId
          Version = AggregateVersion version
          Provenance =
            { OccurredAt = now.AddSeconds(float version)
              Actor = actor
              CorrelationId = correlation
              FormatVersion = Events.formatVersion
              SemanticVersion = Events.semanticVersion }
          Payload = CoreEvent event }

    let started: InvestigationStarted =
        { Title = text "Payment failures"; ObservationId = ObservationId(guid 2); Original = text "Checkout fails" }

    let history = [ envelope 1L (guid 500) (InvestigationStarted started) ]

    let investigation =
        match Kernel.replay history with
        | Ok s -> s
        | Error e -> failwithf "fixture history does not replay: %A" e

    /// A history one other writer extended with an observation (the GitHub version after a concurrent save).
    let otherWritersHistory =
        history
        @ [ envelope 2L (guid 501) (ObservationRecorded { ObservationId = ObservationId(guid 3); Original = text "other writer"; ObservedAt = None }) ]

    let otherWritersInvestigation =
        match Kernel.replay otherWritersHistory with
        | Ok s -> s
        | Error e -> failwithf "fixture history does not replay: %A" e

    /// An unsaved command: one more observation, decided against the base version.
    let edit (n: int) : CommandEnvelope =
        { CommandId = guid (2000 + n)
          Investigation = investigationId
          ExpectedVersion = None
          Command = Core(RecordObservation { ObservationId = ObservationId(guid (3000 + n)); Original = text $"edit {n}"; ObservedAt = None }) }

    let writable =
        WorkspaceAddress.workingCopy [] "t12-fixture"
        |> Option.bind Writable.ofAddress
        |> Option.defaultWith (fun () -> failwith "working copy refused")

    let bytes (tag: byte) : FileSet = { Manifest = [| tag; 1uy |]; Events = [| tag; 2uy |]; Snapshot = [| tag; 3uy |] }

    let verifiedAt (commit: string) (inv: Investigation) (h: EventEnvelope list) (tag: byte) : VerifiedInvestigation =
        { Investigation = inv; History = h; ReadAt = CommitOid commit; Address = Writable.address writable; Bytes = bytes tag }

    let baseVerified = verifiedAt "c0" investigation history 0uy
    let remoteVerified = verifiedAt "c2" otherWritersInvestigation otherWritersHistory 2uy

    let context (next: int64) : RequestContext =
        { Connection =
            { Repository = { Owner = "owner"; Name = "repo"; Branch = "main" }
              Credential = Credential.create "fixture-token" }
          NextId = RequestId next
          MutationInFlightElsewhere = false
          Now = now }

    /// The confirmation a matching read-back produces (the only constructor; PERSIST-023).
    let confirmation (commit: string) =
        match Confirmation.ofReadBack investigationId (CommitOid commit) now (bytes 1uy) (bytes 1uy) with
        | Some c -> c
        | None -> failwith "equal bytes must confirm"

    /// The SM-SAVE implementation persistence publishes: the one public value of type SaveMachine in
    /// Causa.Persistence (T19). Looked up by type so these tests compile before it exists, and fail until it does.
    let tryMachine () : SaveMachine option =
        typeof<SaveMachine>.Assembly.GetTypes()
        |> Seq.collect (fun t -> t.GetProperties(BindingFlags.Public ||| BindingFlags.Static))
        |> Seq.filter (fun p -> p.PropertyType = typeof<SaveMachine> && p.GetIndexParameters().Length = 0)
        |> Seq.tryHead
        |> Option.map (fun p -> nonNull (p.GetValue(null)) :?> SaveMachine)

    let machine () : SaveMachine =
        match tryMachine () with
        | Some m -> m
        | None ->
            failwith
                "No SM-SAVE implementation: Causa.Persistence exposes no public value of type SaveMachine (SL-T19 pending). This test is written first and must fail until it does."

    let stateName (s: SaveState) =
        match s with
        | SaveState.Saved _ -> "Saved"
        | SaveState.Unsaved -> "Unsaved"
        | SaveState.Saving -> "Saving"
        | SaveState.Offline -> "Offline"
        | SaveState.Conflict _ -> "Conflict"
        | SaveState.UnknownOutcome _ -> "UnknownOutcome"
        | SaveState.SaveFailed _ -> "SaveFailed"

    let inputName (i: SaveInput) =
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

    /// The request every outstanding fixture session awaits.
    let outstandingId = RequestId 7L

    /// One representative of every SM-SAVE event (state-machine-contracts.json, SM-SAVE events, 27).
    let inputs: SaveInput list =
        [ SaveInput.Edit(edit 9)
          SaveInput.SaveRequested
          SaveInput.MutationSent outstandingId
          SaveInput.ReadBackMatched(confirmation "c1")
          SaveInput.ReadBackMismatched(CommitOid "c1")
          SaveInput.StaleHeadUnrelated(CommitOid "c2")
          SaveInput.StaleHeadSameInvestigation(CommitOid "c2")
          SaveInput.ProviderRefused(ValidationFailed [ "refused" ])
          SaveInput.CredentialRefused
          SaveInput.AnswerLost
          SaveInput.FailedBeforeSending
          SaveInput.ReconcileMarkerBytesMatch(confirmation "c1")
          SaveInput.ReconcileMarkerBytesDiffer(CommitOid "c1")
          SaveInput.ReconcileNothingWrittenHeadUnchanged
          SaveInput.ReconcileNothingWrittenHeadMoved(CommitOid "c2")
          SaveInput.ReconcileHistoryRewritten
          SaveInput.ReconcileReadFailed
          SaveInput.ReconcileReadsSpent
          SaveInput.CheckAgain
          SaveInput.WentOffline
          SaveInput.WentOnline
          SaveInput.ConflictResolvedReapplied [ edit 1 ]
          SaveInput.ConflictResolvedDiscarded remoteVerified
          SaveInput.SupersededReply(RequestId 3L)
          SaveInput.QueuedSaveStarted
          SaveInput.RebaseReadFailed
          SaveInput.ReadBackReadFailed ]

    let states = [ "Saved"; "Unsaved"; "Saving"; "Offline"; "Conflict"; "UnknownOutcome"; "SaveFailed" ]

    /// The legal (state, event) pairs of SM-SAVE and every next state their guarded transitions may reach
    /// (requirements-compiled/v4.1.5/state-machine-contracts.json: 50 legal pairs, 139 illegal).
    let legal: Map<string * string, string list> =
        Map.ofList
            [ ("Saved", "Edit"), [ "Unsaved"; "Offline" ]
              ("Saved", "WentOffline"), [ "Saved" ]
              ("Saved", "WentOnline"), [ "Saved" ]
              ("Unsaved", "Edit"), [ "Unsaved" ]
              ("Unsaved", "SaveRequested"), [ "Saving"; "Offline"; "Unsaved" ]
              ("Unsaved", "WentOffline"), [ "Offline" ]
              ("Unsaved", "WentOnline"), [ "Unsaved" ]
              ("Unsaved", "QueuedSaveStarted"), [ "Saving"; "Offline" ]
              ("Saving", "Edit"), [ "Saving" ]
              ("Saving", "MutationSent"), [ "Saving" ]
              ("Saving", "ReadBackMatched"), [ "Saved"; "Unsaved" ]
              ("Saving", "ReadBackMismatched"), [ "SaveFailed" ]
              ("Saving", "StaleHeadUnrelated"), [ "Saving"; "SaveFailed" ]
              ("Saving", "StaleHeadSameInvestigation"), [ "Conflict" ]
              ("Saving", "ProviderRefused"), [ "SaveFailed" ]
              ("Saving", "CredentialRefused"), [ "SaveFailed" ]
              ("Saving", "AnswerLost"), [ "UnknownOutcome" ]
              ("Saving", "FailedBeforeSending"), [ "Offline" ]
              ("Saving", "WentOffline"), [ "Saving" ]
              ("Saving", "WentOnline"), [ "Saving" ]
              ("Saving", "SupersededReply"), [ "Saving" ]
              ("Saving", "RebaseReadFailed"), [ "Offline"; "UnknownOutcome" ]
              ("Saving", "ReadBackReadFailed"), [ "UnknownOutcome" ]
              ("Offline", "Edit"), [ "Offline" ]
              ("Offline", "SaveRequested"), [ "Offline" ]
              ("Offline", "WentOffline"), [ "Offline" ]
              ("Offline", "WentOnline"), [ "Unsaved" ]
              ("Offline", "QueuedSaveStarted"), [ "Offline" ]
              ("Conflict", "Edit"), [ "Conflict" ]
              ("Conflict", "WentOffline"), [ "Conflict" ]
              ("Conflict", "WentOnline"), [ "Conflict" ]
              ("Conflict", "ConflictResolvedReapplied"), [ "Unsaved" ]
              ("Conflict", "ConflictResolvedDiscarded"), [ "Saved" ]
              ("UnknownOutcome", "Edit"), [ "UnknownOutcome" ]
              ("UnknownOutcome", "StaleHeadSameInvestigation"), [ "Conflict" ]
              ("UnknownOutcome", "ReconcileMarkerBytesMatch"), [ "Saved"; "Unsaved" ]
              ("UnknownOutcome", "ReconcileMarkerBytesDiffer"), [ "SaveFailed" ]
              ("UnknownOutcome", "ReconcileNothingWrittenHeadUnchanged"), [ "Saving"; "SaveFailed" ]
              ("UnknownOutcome", "ReconcileNothingWrittenHeadMoved"), [ "Saving"; "SaveFailed" ]
              ("UnknownOutcome", "ReconcileHistoryRewritten"), [ "SaveFailed" ]
              ("UnknownOutcome", "ReconcileReadFailed"), [ "UnknownOutcome" ]
              ("UnknownOutcome", "ReconcileReadsSpent"), [ "UnknownOutcome" ]
              ("UnknownOutcome", "CheckAgain"), [ "UnknownOutcome" ]
              ("UnknownOutcome", "WentOffline"), [ "UnknownOutcome" ]
              ("UnknownOutcome", "WentOnline"), [ "UnknownOutcome" ]
              ("UnknownOutcome", "SupersededReply"), [ "UnknownOutcome" ]
              ("SaveFailed", "Edit"), [ "SaveFailed" ]
              ("SaveFailed", "SaveRequested"), [ "Saving"; "Offline" ]
              ("SaveFailed", "WentOffline"), [ "SaveFailed" ]
              ("SaveFailed", "WentOnline"), [ "SaveFailed" ] ]

    /// The inputs that may move a session into Saved from another state: a read-back confirmation, a reconciliation
    /// that found this save's marker on equal bytes, or a confirmed discard to a verified GitHub version (ODR-V4-08).
    let savedEntries = set [ "ReadBackMatched"; "ReconcileMarkerBytesMatch"; "ConflictResolvedDiscarded" ]

    /// What the session awaits for an input in Saving or UnknownOutcome, so the input is the current step's answer.
    let private awaitingFor (input: SaveInput) : Awaiting =
        match input with
        | SaveInput.ReadBackMatched _
        | SaveInput.ReadBackMismatched _
        | SaveInput.ReadBackReadFailed -> ReadBackAt(CommitOid "c1")
        | SaveInput.StaleHeadUnrelated _
        | SaveInput.StaleHeadSameInvestigation _ -> FileComparisonAt(CommitOid "c2")
        | SaveInput.RebaseReadFailed -> FreshHead
        | SaveInput.ReconcileMarkerBytesMatch _
        | SaveInput.ReconcileMarkerBytesDiffer _ -> ReconcileFilesAt(CommitOid "c1")
        | SaveInput.ReconcileNothingWrittenHeadUnchanged
        | SaveInput.ReconcileNothingWrittenHeadMoved _
        | SaveInput.ReconcileHistoryRewritten
        | SaveInput.ReconcileReadFailed -> ReconcileCommits 1
        | _ -> MutationAnswer

    /// A session in the named state whose guards make the input's transition (if legal) applicable: online, no other
    /// mutation in flight, no change during the save, rebase and mutation limits not reached, automatic
    /// reconciliation reads spent (the guard of CheckAgain), a conflict already resolved for the resolution events.
    let sessionIn (m: SaveMachine) (state: string) (input: SaveInput) : InvestigationSession =
        let saved = m.Initial writable baseVerified
        let pending = { saved with Unsaved = [ edit 1 ] }
        let waiting = { pending with Outstanding = Some(outstandingId, awaitingFor input); Mutations = 1 }
        match state with
        | "Saved" -> saved
        | "Unsaved" -> { pending with State = SaveState.Unsaved }
        | "Saving" -> { waiting with State = SaveState.Saving }
        | "Offline" -> { pending with State = SaveState.Offline }
        | "Conflict" ->
            let view =
                match input with
                | SaveInput.ConflictResolvedReapplied _ -> Reapplied(remoteVerified, [])
                | SaveInput.ConflictResolvedDiscarded _ -> Discarded remoteVerified
                | _ -> Reviewable remoteVerified
            { pending with State = SaveState.Conflict view }
        | "UnknownOutcome" -> { waiting with State = SaveState.UnknownOutcome 0; ReconcileReads = 3 }
        | "SaveFailed" -> { pending with State = SaveState.SaveFailed RepositoryKeptChanging }
        | other -> failwithf "unknown state %s" other

    /// INV-01's oracle over read-back confirmation: for every (written, re-read) pair, a confirmation exists exactly
    /// when the bytes are equal, and it names the commit that was read back. Returns the violations.
    let confirmationViolations
        (ofReadBack: InvestigationId -> CommitOid -> DateTimeOffset -> FileSet -> FileSet -> Confirmation.T option)
        : string list =
        let variants (f: FileSet) =
            [ f
              { f with Manifest = Array.append f.Manifest [| 9uy |] }
              { f with Events = [| 0uy |] }
              { f with Snapshot = Array.empty }
              { Manifest = Array.copy f.Manifest; Events = Array.copy f.Events; Snapshot = Array.copy f.Snapshot } ]
        [ for tag in 0uy .. 4uy do
              let written = bytes tag
              for i, reread in List.indexed (variants written) do
                  let commit = CommitOid $"c{tag}-{i}"
                  let equal = written.Manifest = reread.Manifest && written.Events = reread.Events && written.Snapshot = reread.Snapshot
                  match ofReadBack investigationId commit now written reread, equal with
                  | Some c, true when c.CommitOid = commit && c.InvestigationId = investigationId -> ()
                  | Some _, true -> yield $"confirmation for {commit} names another commit or investigation"
                  | None, true -> yield $"equal bytes at {commit} were not confirmed"
                  | Some _, false -> yield $"different bytes at {commit} were confirmed"
                  | None, false -> () ]

namespace Causa.Invariants.Model

open Xunit
open Causa.Persistence
open Causa.Invariants
open Causa.Invariants.SaveFixture

/// INV-08: no action replaces the GitHub version with the tab's version; conflict resolution offers only review,
/// reapply through the engine and confirmed discard.
module INV08 =
    let private conflictInputs: ConflictInput list =
        [ ConflictInput.RemoteVerified remoteVerified
          ConflictInput.RemoteReadFailed LostAfterSending
          ConflictInput.RemoteMissingOrInvalid None
          ConflictInput.ReadAgain
          ConflictInput.ReviewChosen
          ConflictInput.ReapplyChosen
          ConflictInput.ReapplyDone([ edit 1 ], [])
          ConflictInput.DiscardChosen
          ConflictInput.DiscardConfirmed
          ConflictInput.DiscardCancelled
          ConflictInput.ExportChosen
          ConflictInput.SupersededReply(RequestId 3L) ]

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

    /// A request that writes to GitHub: a GraphQL mutation (createCommitOnBranch) or any REST write.
    let private writes (r: HttpRequest) =
        let body = r.Body |> Option.defaultValue ""
        body.Contains("mutation") || body.Contains("createCommitOnBranch")
        || (r.Method <> Get && not (r.Url.EndsWith("/graphql")))

    [<Fact>]
    let ConflictActionsNeverOverwrite () =
        // The action set itself: exactly review, reapply, discard (with confirmation), export, re-read; no overwrite.
        let caseNames =
            Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<ConflictInput>) |> Array.map (fun c -> c.Name) |> set
        Assert.Equal<Set<string>>(
            set [ "RemoteVerified"; "RemoteReadFailed"; "RemoteMissingOrInvalid"; "ReadAgain"; "ReviewChosen"; "ReapplyChosen"
                  "ReapplyDone"; "DiscardChosen"; "DiscardConfirmed"; "DiscardCancelled"; "ExportChosen"; "SupersededReply" ],
            caseNames)
        // Every reachable SM-CONFLICT state, under every input, emits no write and never turns into the tab's version.
        let m = machine ()
        let rec explore (seen: Set<string>) (frontier: ConflictView list) (violations: string list) =
            match frontier with
            | [] -> seen, violations
            | v :: rest when seen.Contains(viewName v) -> explore seen rest violations
            | v :: rest ->
                let outcomes = conflictInputs |> List.map (fun i -> i, m.StepConflict (context 20L) v i)
                let found =
                    [ for i, r in outcomes do
                          match r with
                          | Ok(_, effects) ->
                              for e in effects do
                                  match e with
                                  | SaveEffect.Send req when writes req -> yield $"{viewName v} + {i}: sends a write {req.Url}"
                                  | _ -> ()
                          | Error _ -> () ]
                let next = outcomes |> List.choose (fun (_, r) -> match r with Ok(v', _) -> Some v' | Error _ -> None)
                explore (seen.Add(viewName v)) (rest @ next) (violations @ found)
        let seen, violations = explore Set.empty [ ReadingRemote(RequestId 7L); Reviewable remoteVerified ] []
        Assert.Empty(violations)
        // The resolution states are reachable, so the exploration exercised them.
        Assert.Contains("Reviewable", seen)
        Assert.Contains("ConfirmingDiscard", seen)
