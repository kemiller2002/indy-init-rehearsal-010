// Owner: domain. IF41-DOMAIN producer contract tests.
namespace Causa.Contract.Tests.Domain

open System
open Xunit
open Causa.Domain

module Producer =
    let private guid (n: int) = Guid(sprintf "00000000-0000-4000-8000-%012d" n)

    let private text (s: string) =
        match NonEmptyText.create s with
        | Ok t -> t
        | Error e -> failwith e

    let private inv = InvestigationId(guid 1)
    let private at = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)
    let private actor: ActorContext = { ActorId = "tester" }
    let private policy = Transitions.corePolicy

    let private command (n: int) (expected: AggregateVersion option) (c: CoreCommand) : CommandEnvelope =
        { CommandId = guid (10000 + n); Investigation = inv; ExpectedVersion = expected; Command = Core c }

    /// Appends an Allowed decision's events as the application would (provenance from the command; no clock).
    let private append (state: Investigation option) (cmd: CommandEnvelope) (events: EventPayload list) =
        let start = state |> Option.map (fun s -> AggregateVersion.value s.Version) |> Option.defaultValue 0L
        events
        |> List.mapi (fun i payload ->
            let v = start + int64 (i + 1)
            { EventId = EventId(guid (50000 + int v))
              Investigation = inv
              Version = AggregateVersion v
              Provenance =
                { OccurredAt = at.AddSeconds(float v)
                  Actor = { ActorId = text actor.ActorId; Kind = ActorKind.Human }
                  CorrelationId = cmd.CommandId
                  FormatVersion = Events.formatVersion
                  SemanticVersion = Events.semanticVersion }
              Payload = payload })

    /// Runs commands through the kernel; every one must be Allowed. Returns the state and the history.
    let private run (commands: CoreCommand list) =
        commands
        |> List.indexed
        |> List.fold
            (fun (state: Investigation option, history: EventEnvelope list) (i, c) ->
                let cmd = command i None c
                match Kernel.domain.Decide policy actor state cmd with
                | Allowed events ->
                    let envs = append state cmd events
                    let next =
                        envs
                        |> List.fold (fun s e -> match Kernel.domain.Evolve s e with Ok x -> Some x | Error err -> failwithf "%A" err) state
                    next, history @ envs
                | other -> failwithf "fixture command %s was not allowed: %A" (Commands.name c) other)
            (None, [])

    let private h n = HypothesisId(guid n)
    let private ev n = EvidenceId(guid n)
    let private mark n : MarkHypothesis = { HypothesisId = h n; Reason = text "basis"; EvidenceIds = [ ev 30 ] }

    let private start: StartInvestigation = { Title = text "Payment failures"; ObservationId = ObservationId(guid 2); Original = text "Checkout fails" }

    /// One representative of every CoreCommand case (29) plus the extension command.
    let private everyCommand: Command list =
        [ Core(StartInvestigation start)
          Core ActivateInvestigation
          Core(RecordObservation { ObservationId = ObservationId(guid 3); Original = text "o"; ObservedAt = None })
          Core(RecordHypothesis { HypothesisId = h 40; Statement = text "h"; Mechanism = None })
          Core(MarkHypothesisPlausible(mark 20))
          Core(MarkHypothesisSupported(mark 21))
          Core(FalsifyHypothesis(mark 22))
          Core(ReopenHypothesis(mark 23))
          Core(AdmitEvidence { EvidenceId = ev 41; ContentKind = ContentKind.Text; Content = text "e"; Category = EvidenceCategory.Log; Source = text "s"; CapturedAt = None })
          Core(RelateEvidence { RelationshipId = EvidenceRelationshipId(guid 42); EvidenceId = ev 30; HypothesisId = h 20; Kind = RelationshipKind.Supports; Rationale = None })
          Core(IdentifyUnknown { UnknownId = UnknownId(guid 43); Question = text "q"; Materiality = Materiality.Material })
          Core(RecordAssumption { AssumptionId = AssumptionId(guid 44); Proposition = text "p"; Materiality = Materiality.Blocking })
          Core(RecordContradiction { ContradictionId = ContradictionId(guid 45); Statement = text "c"; EvidenceIds = [ ev 30 ]; Materiality = Materiality.Material })
          Core(RecordPrediction { PredictionId = PredictionId(guid 46); HypothesisId = h 20; Statement = text "p"; Strength = PredictionStrength.Expected; Timing = PredictionTiming.Prospective })
          Core(RecordInvestigationAction { ActionId = InvestigationActionId(guid 47); Kind = ActionKind.Test; Purpose = text "a"; TargetHypothesisIds = [ h 20 ] })
          Core(RecordFinding { FindingId = FindingId(guid 48); ActionId = InvestigationActionId(guid 31); OriginalResult = text "r"; Interpretation = None })
          Core(EstablishNegativeKnowledge { NegativeKnowledgeId = NegativeKnowledgeId(guid 49); Statement = text "n"; Scope = text "s"; BasisEvidenceIds = [ ev 30 ] })
          Core(RecordCausalAssertion { CauseId = CausalAssertionId(guid 50); Outcome = text "o"; Members = [ { HypothesisId = h 21; Role = CauseRole.RootCause } ]; Mechanism = None })
          Core(ConfirmCause { CauseId = CausalAssertionId(guid 32); Policy = text "core" })
          Core(RecordCorrectiveAction { CorrectiveActionId = CorrectiveActionId(guid 51); Kind = CorrectiveActionKind.Correction; Description = text "d"; TargetCauseIds = [ CausalAssertionId(guid 32) ] })
          Core(RecordVerificationCriterion { CriterionId = VerificationCriterionId(guid 52); CorrectiveActionId = CorrectiveActionId(guid 33); ExpectedOutcome = text "x"; Method = None })
          Core(MarkCorrectiveActionImplemented(CorrectiveActionId(guid 33)))
          Core(RecordVerification { VerificationId = VerificationId(guid 53); CriterionId = VerificationCriterionId(guid 34); Outcome = VerificationOutcome.Satisfied; EvidenceIds = [ ev 30 ] })
          Core ResolveInvestigation
          Core(ReopenInvestigation { Reason = text "new evidence" })
          Core(RecordProposal { ProposalId = ProposalId(guid 54); Kind = ProposalKind.Hypothesis; ProposalText = text "p"; Rationale = None })
          Core(AcceptProposal(ProposalId(guid 35)))
          Core(RejectProposal(ProposalId(guid 35), text "no"))
          Core(AttachExtension { SemanticId = { Namespace = text "edf"; Name = text "x"; Version = text "1" }; SchemaId = "s"; Stability = StabilityClass.Experimental; Requiredness = Requiredness.Optional; PayloadJson = "{}" })
          Extension({ Namespace = text "edf"; Name = text "y"; Version = text "1" }, "{}") ]

    /// A state with every entity kind a command may reference.
    let private rich, richHistory =
        run
            [ StartInvestigation start
              ActivateInvestigation
              AdmitEvidence { EvidenceId = ev 30; ContentKind = ContentKind.Text; Content = text "e"; Category = EvidenceCategory.Log; Source = text "s"; CapturedAt = None }
              RecordHypothesis { HypothesisId = h 20; Statement = text "h20"; Mechanism = None }
              RecordHypothesis { HypothesisId = h 21; Statement = text "h21"; Mechanism = None }
              RecordHypothesis { HypothesisId = h 22; Statement = text "h22"; Mechanism = None }
              RecordHypothesis { HypothesisId = h 23; Statement = text "h23"; Mechanism = None }
              MarkHypothesisPlausible(mark 21)
              FalsifyHypothesis(mark 23)
              RecordInvestigationAction { ActionId = InvestigationActionId(guid 31); Kind = ActionKind.Query; Purpose = text "a"; TargetHypothesisIds = [] }
              RecordCausalAssertion { CauseId = CausalAssertionId(guid 32); Outcome = text "o"; Members = [ { HypothesisId = h 21; Role = CauseRole.Trigger } ]; Mechanism = None }
              RecordCorrectiveAction { CorrectiveActionId = CorrectiveActionId(guid 33); Kind = CorrectiveActionKind.Mitigation; Description = text "d"; TargetCauseIds = [] }
              RecordVerificationCriterion { CriterionId = VerificationCriterionId(guid 34); CorrectiveActionId = CorrectiveActionId(guid 33); ExpectedOutcome = text "x"; Method = None }
              RecordProposal { ProposalId = ProposalId(guid 35); Kind = ProposalKind.Unknown; ProposalText = text "p"; Rationale = None } ]

    let private fresh, _ = run [ StartInvestigation start ]

    [<Fact>]
    let DecideIsTotalOverCommands () =
        // The representatives cover every case of the frozen CoreCommand union.
        let cases = Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<CoreCommand>) |> Array.map (fun c -> c.Name) |> set
        let covered = everyCommand |> List.choose (function Core c -> Some(Commands.name c) | Extension _ -> None) |> set
        Assert.Equal<Set<string>>(cases, covered)
        let states = [ "none", None; "draft", fresh; "active", rich ]
        let decisions =
            [ for name, state in states do
                  for i, c in List.indexed everyCommand do
                      let version = state |> Option.map (fun s -> s.Version)
                      let cmd = { CommandId = guid (90000 + i); Investigation = inv; ExpectedVersion = version; Command = c }
                      yield name, c, state, cmd, Kernel.domain.Decide policy actor state cmd ]
        // Total: a decision for every (state, command), and every Allowed decision's events evolve.
        Assert.Equal(states.Length * everyCommand.Length, decisions.Length)
        for name, _, state, cmd, decision in decisions do
            match decision with
            | Allowed events ->
                Assert.NotEmpty(events)
                let applied = append state cmd events |> List.fold (fun s e -> s |> Result.bind (fun x -> Kernel.domain.Evolve x e |> Result.map Some)) (Ok state)
                Assert.True(Result.isOk applied, $"{name}: allowed events do not evolve: {applied}")
            | _ -> ()
        let decisionOf stateName (c: Command) =
            decisions |> List.pick (fun (n, c', _, _, d) -> if n = stateName && c' = c then Some d else None)
        // StartInvestigation only without an investigation; every other core command needs one.
        Assert.True(match decisionOf "none" (Core(StartInvestigation start)) with Allowed _ -> true | _ -> false)
        Assert.True(match decisionOf "active" (Core(StartInvestigation start)) with Blocked _ -> true | _ -> false)
        Assert.True(match decisionOf "none" (Core ActivateInvestigation) with Blocked _ -> true | _ -> false)
        // In the rich state, each structurally legal command is allowed.
        for c in everyCommand.[1..25] |> List.filter (fun c -> c <> Core ActivateInvestigation && c <> Core(ReopenInvestigation { Reason = text "new evidence" })) do
            let ok = match decisionOf "active" c with Allowed _ -> true | _ -> false
            let expected =
                match c with
                | Core(MarkCorrectiveActionImplemented _) -> false // the corrective action is Proposed, not InProgress
                | _ -> true
            let actual = decisionOf "active" c
            Assert.True((ok = expected), $"active: {c} -> {actual}")
        // Extensions are unsupported semantics, never silently applied.
        Assert.True(match decisionOf "active" everyCommand.[28] with UnsupportedSemantics _ -> true | _ -> false)
        Assert.True(match decisionOf "active" everyCommand.[29] with UnsupportedSemantics _ -> true | _ -> false)
        // A deliberately invalid case fails for the intended reason: falsifying an already falsified hypothesis.
        let refalsify = command 99 None (FalsifyHypothesis(mark 23))
        match Kernel.domain.Decide policy actor rich refalsify with
        | Blocked [ b ] -> Assert.Equal("transition.illegal", b.Code)
        | other -> failwithf "re-falsifying was %A" other
        // A stale expected version is a Conflict; a repeated command is a Duplicate.
        let stale = command 98 (Some(AggregateVersion 1L)) (RecordObservation { ObservationId = ObservationId(guid 60); Original = text "o"; ObservedAt = None })
        Assert.Equal(Conflict(AggregateVersion 1L, rich.Value.Version), Kernel.domain.Decide policy actor rich stale)
        let repeated = command 2 None (RecordObservation { ObservationId = ObservationId(guid 61); Original = text "o"; ObservedAt = None })
        match Kernel.domain.Decide policy actor rich repeated with
        | Duplicate [ EventId _ ] -> ()
        | other -> failwithf "a repeated command was %A" other

    [<Fact>]
    let ReplayRefusesGaps () =
        Assert.True(richHistory.Length >= 10)
        // The whole history replays to the state it was built from.
        Assert.Equal(Ok rich.Value, Kernel.domain.Replay richHistory)
        Assert.Equal(Error EmptyHistory, Kernel.domain.Replay [])
        // A missing version, a duplicated version and a history that does not start at 1 are refused, never patched.
        let without2 = richHistory |> List.filter (fun e -> e.Version <> AggregateVersion 2L)
        Assert.Equal(Error(NonContiguousVersion(AggregateVersion 2L, AggregateVersion 3L)), Kernel.domain.Replay without2)
        let duplicated = richHistory.[0..2] @ [ richHistory.[2] ] @ richHistory.[3..]
        Assert.Equal(Error(NonContiguousVersion(AggregateVersion 4L, AggregateVersion 3L)), Kernel.domain.Replay duplicated)
        Assert.Equal(Error(NonContiguousVersion(AggregateVersion 1L, AggregateVersion 2L)), Kernel.domain.Replay richHistory.Tail)
        // An event kind causa-storage v1.0.0 does not have is refused by name, not skipped.
        let ext = { richHistory.[1] with Payload = ExtensionEvent({ Namespace = text "edf"; Name = text "x"; Version = text "1" }, "{}") }
        Assert.Equal(Error(UnknownEventKind "edf/x@1"), Kernel.domain.Replay(richHistory.[0] :: ext :: richHistory.[2..]))
        // A contiguous history with an illegal event (a transition whose 'from' is not the replayed state) is refused.
        let forged =
            richHistory
            |> List.map (fun e ->
                match e.Payload with
                | CoreEvent(HypothesisFalsified t) -> { e with Payload = CoreEvent(HypothesisFalsified { t with From = HypothesisState.Supported }) }
                | _ -> e)
        match Kernel.domain.Replay forged with
        | Error(IllegalEvent err) -> Assert.Contains("from", err.Reason)
        | other -> failwithf "a forged transition replayed as %A" other
