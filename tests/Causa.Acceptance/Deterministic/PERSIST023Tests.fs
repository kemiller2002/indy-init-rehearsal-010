// Owner: domain. PERSIST-023: truthful save state is structural (PAT-12).
namespace Causa.Acceptance.Deterministic

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

    /// Every (state, event) pair of SM-SAVE with the machine's answer: 7 states x 27 events = 189 pairs.
    let decideAll (m: SaveMachine) =
        [ for state in states do
              for input in inputs do
                  let session = sessionIn m state input
                  yield state, input, session, m.Step (context 40L) session input ]

    /// PERSIST-051-O2 / PERSIST-023-O4 over the full enumeration: legal pairs reach a state the contract allows;
    /// illegal pairs fail closed with a typed fault naming the machine, state and event; Saved is entered only from a
    /// read-back confirmation (or the approved verified-discard basis). Returns the violations.
    let pairViolations (m: SaveMachine) =
        [ for state, input, session, result in decideAll m do
              let name = inputName input
              match legal.TryFind((state, name)), result with
              | Some allowedNext, Ok(next, _) ->
                  let reached = stateName next.State
                  if not (List.contains reached allowedNext) then
                      yield $"{state} + {name}: reached {reached}, contract allows {allowedNext}"
                  match next.State with
                  | SaveState.Saved basis when state <> "Saved" ->
                      match basis, input with
                      | Confirmed c, SaveInput.ReadBackMatched expected
                      | Confirmed c, SaveInput.ReconcileMarkerBytesMatch expected when c = expected -> ()
                      | VerifiedAt v, SaveInput.ConflictResolvedDiscarded expected when v = expected -> ()
                      | _ -> yield $"{state} + {name}: Saved without the read-back confirmation it was given"
                  | _ -> ()
              | Some _, Error fault -> yield $"{state} + {name}: legal pair refused ({fault.Reason})"
              | None, Ok(next, _) -> yield $"{state} + {name}: illegal pair accepted, reached {stateName next.State}"
              | None, Error fault ->
                  if fault.Machine <> "SM-SAVE" || not (fault.State.Contains state) || not (fault.Input.Contains name) then
                      yield $"{state} + {name}: fault does not name the machine, state and event: {fault}"
                  ignore session ]


/// PERSIST-023 (PAT-12): the save state is computed by one F# machine; Saved rests only on a read-back confirmation.
module PERSIST023Tests =
    open Xunit
    open SaveFixture

    let private machineValues () =
        typeof<SaveMachine>.Assembly.GetTypes()
        |> Seq.collect (fun t -> t.GetProperties(BindingFlags.Public ||| BindingFlags.Static))
        |> Seq.filter (fun p -> p.PropertyType = typeof<SaveMachine>)
        |> List.ofSeq

    let private caseNames (t: Type) =
        Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(t) |> Array.map (fun c -> c.Name) |> List.ofArray

    [<Fact>]
    let ``PERSIST-023-O1`` () =
        // One machine: exactly one SaveMachine value is published, and its state union is the seven PERSIST-031 states.
        Assert.Equal(1, (machineValues ()).Length)
        Assert.Equal<string list>(states, caseNames typeof<SaveState>)
        // The interface renders only that machine's projection: IF41-VIEW's save indicator reads an
        // InvestigationSession (the machine's state), nothing else.
        let field = typeof<Causa.App.Projections>.GetProperty("SaveIndicator") |> nonNull
        Assert.Equal(typeof<InvestigationSession -> Causa.App.SaveIndicatorView>, field.PropertyType)
        // The state is the machine's: Saved at load, Unsaved after an edit, and only Step moves it.
        let m = machine ()
        let initial = m.Initial writable baseVerified
        Assert.Equal("Saved", stateName initial.State)
        match m.Step (context 1L) initial (SaveInput.Edit(edit 1)) with
        | Ok(next, _) ->
            Assert.Contains(stateName next.State, [ "Unsaved"; "Offline" ])
            Assert.Contains(edit 1, next.Unsaved)
        | Error f -> failwithf "an edit of a saved investigation was refused: %A" f

    [<Fact>]
    let ``PERSIST-023-O2`` () =
        // The only confirmation constructor confirms exactly the equal re-reads.
        Assert.Empty(confirmationViolations Confirmation.ofReadBack)
        // Over every state and event, Saved is entered only with the confirmation the read-back produced.
        let m = machine ()
        let savedViolations =
            pairViolations m |> List.filter (fun v -> v.Contains "Saved without" || v.Contains "reached Saved")
        Assert.Empty(savedViolations)
        // A read-back that differs leaves the save failed with the commit named, never Saved.
        let saving = sessionIn m "Saving" (SaveInput.ReadBackMismatched(CommitOid "c1"))
        match m.Step (context 2L) saving (SaveInput.ReadBackMismatched(CommitOid "c1")) with
        | Ok(next, _) -> Assert.Equal(SaveState.SaveFailed(BytesDiffer(CommitOid "c1")), next.State)
        | Error f -> failwithf "a mismatched read-back was refused: %A" f

    [<Fact>]
    let ``PERSIST-023-O3`` () =
        // Confirmation.T has a private representation: no public constructor, no public settable state, and the only
        // public function that returns one is Confirmation.ofReadBack.
        let t = typeof<Confirmation.T>
        Assert.Empty(t.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance))
        Assert.Empty(t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance) |> Array.filter (fun p -> p.CanWrite))
        Assert.Empty(t.GetMethods(BindingFlags.Public ||| BindingFlags.Static) |> Array.filter (fun mi -> mi.ReturnType = t))
        let producers =
            (nonNull t.DeclaringType).GetMethods(BindingFlags.Public ||| BindingFlags.Static)
            |> Array.filter (fun mi -> mi.ReturnType = t || mi.ReturnType = typeof<Confirmation.T option>)
            |> Array.map (fun mi -> mi.Name)
        Assert.Equal<string[]>([| "ofReadBack" |], producers)
        // No assembly (adapters, views, the host, test fixtures) is granted Causa.Persistence's internals.
        let grants = typeof<SaveMachine>.Assembly.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
        Assert.Empty(grants)

    [<Fact>]
    let ``PERSIST-023-O4`` () =
        let m = machine ()
        let illegal = decideAll m |> List.filter (fun (s, i, _, _) -> not (legal.ContainsKey((s, inputName i))))
        Assert.Equal(139, illegal.Length)
        Assert.All(illegal, fun (_, _, _, result) -> Assert.True(Result.isError result))
        Assert.Empty(pairViolations m |> List.filter (fun v -> v.Contains "illegal" || v.Contains "fault"))

    [<Fact>]
    let ``PERSIST-023-O5`` () =
        // PAT-12: every state and input pair is decided as the contract says, and application code cannot forge the
        // confirmation Saved rests on (the reflection and compile checks are PERSIST-023-O3 and PERSIST-051-O3).
        let m = machine ()
        Assert.Equal(189, (decideAll m).Length)
        Assert.Empty(pairViolations m)
        Assert.Empty(typeof<Confirmation.T>.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance))
