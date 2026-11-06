// Owner: ui-3. Investigations for the analysis-section tests, built only through the real kernel: each command is
// decided by Kernel.domain.Decide under the core policy and its events are applied with Kernel.domain.Evolve, as the
// application does (provenance from the command). No investigation state is written by hand.
namespace Causa.TestSupport.UiFixtures

open System
open Causa.Domain

[<RequireQualifiedAccess>]
module AnalysisScenarios =
    let g (n: int) = Guid(sprintf "00000000-0000-4000-8000-%012d" n)

    let t (s: string) =
        match NonEmptyText.create s with
        | Ok x -> x
        | Error e -> failwith e

    let investigationId = InvestigationId(g 1)
    let actor: ActorContext = { ActorId = "investigator" }
    let startedAt = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)

    /// Decides one command and applies its events; fails the test when the engine does not allow it.
    let submit (state: Investigation option) (n: int) (c: CoreCommand) : Investigation =
        let cmd =
            { CommandId = g (10000 + n); Investigation = investigationId
              ExpectedVersion = state |> Option.map (fun s -> s.Version); Command = Core c }
        match Kernel.domain.Decide Policy.core actor state cmd with
        | Allowed events ->
            let start = state |> Option.map (fun s -> AggregateVersion.value s.Version) |> Option.defaultValue 0L
            events
            |> List.indexed
            |> List.fold
                (fun s (i, payload) ->
                    let v = start + int64 (i + 1)
                    let env =
                        { EventId = EventId(g (50000 + int v)); Investigation = investigationId; Version = AggregateVersion v
                          Provenance =
                            { OccurredAt = startedAt.AddMinutes(float v)
                              Actor = { ActorId = t actor.ActorId; Kind = ActorKind.Human }
                              CorrelationId = cmd.CommandId; FormatVersion = Events.formatVersion; SemanticVersion = Events.semanticVersion }
                          Payload = payload }
                    match Kernel.domain.Evolve s env with
                    | Ok x -> Some x
                    | Error e -> failwithf "allowed event did not evolve: %A" e)
                state
            |> Option.get
        | d -> failwithf "%s was not allowed: %A" (Commands.name c) d

    /// Runs commands in order from no investigation.
    let run (commands: CoreCommand list) : Investigation =
        commands
        |> List.indexed
        |> List.fold (fun (state: Investigation option) (n, c) -> Some(submit state n c)) None
        |> Option.get

    /// Applies further commands to an investigation.
    let continueWith (s: Investigation) (commands: CoreCommand list) : Investigation =
        commands
        |> List.indexed
        |> List.fold (fun (state: Investigation) (n, c) -> submit (Some state) (500 + int (AggregateVersion.value state.Version) + n) c) s

    // The J2 shape, through the commands the frozen contract has.
    let problem = "Checkout payments fail since 09:00"
    let title = "Checkout failures"
    let observationText = "Error rate rose to 12% at 09:02"
    let h1, h2 = HypothesisId(g 20), HypothesisId(g 21)
    let h1Statement, h2Statement = "The TLS certificate expired", "The database is saturated"
    let h1Explanation = "Handshakes fail once the certificate is no longer valid"
    let e1, e2 = EvidenceId(g 30), EvidenceId(g 31)
    let e1Content, e2Content = "handshake_failure entries in the gateway log", "database CPU at 40%"
    let e1Source, e2Source = "https://logs.example.org/gateway/0900", "metrics dashboard"
    let r1, r2, r3 = "handshake failures match the error window", "the database was idle", "the log window is too short to tell"

    let start: StartInvestigation = { Title = t title; ObservationId = ObservationId(g 2); Original = t problem }

    let admit e content source : CoreCommand =
        AdmitEvidence
            { EvidenceId = e; ContentKind = ContentKind.Text; Content = t content; Category = EvidenceCategory.Log
              Source = t source; CapturedAt = None }

    let relate n e h kind rationale : CoreCommand =
        RelateEvidence
            { RelationshipId = EvidenceRelationshipId(g n); EvidenceId = e; HypothesisId = h; Kind = kind
              Rationale = Some(t rationale) }

    /// Started only: no observation, hypothesis or evidence beyond the problem statement.
    let started () = run [ StartInvestigation start ]

    /// An observation, two hypotheses, two evidence items, e1 supports h1 and e2 contradicts h1.
    let analysed () =
        run
            [ StartInvestigation start
              ActivateInvestigation
              RecordObservation { ObservationId = ObservationId(g 3); Original = t observationText; ObservedAt = None }
              RecordHypothesis { HypothesisId = h1; Statement = t h1Statement; Mechanism = Some(t h1Explanation) }
              RecordHypothesis { HypothesisId = h2; Statement = t h2Statement; Mechanism = None }
              admit e1 e1Content e1Source
              admit e2 e2Content e2Source
              relate 40 e1 h1 RelationshipKind.Supports r1
              relate 41 e2 h1 RelationshipKind.Contradicts r2 ]

    /// analysed, then e1 against h1 reassessed (a new assessment of the same pair, INV-035).
    let reassessed () = continueWith (analysed ()) [ relate 42 e1 h1 RelationshipKind.Contradicts r3 ]

    let conclusionStatement = "The expired certificate is the leading explanation"

    /// A conclusion as a loaded history holds it. The frozen contract has no command that records one (INV-032 is an
    /// open owner decision), so the stored ConclusionRecorded event is applied with Kernel.domain.Evolve, exactly as a
    /// history read from GitHub is replayed.
    let withStoredConclusion (s: Investigation) : Investigation =
        let v = AggregateVersion.value s.Version + 1L
        let env =
            { EventId = EventId(g (50000 + int v)); Investigation = investigationId; Version = AggregateVersion v
              Provenance =
                { OccurredAt = startedAt.AddMinutes(float v)
                  Actor = { ActorId = t actor.ActorId; Kind = ActorKind.Human }
                  CorrelationId = g (20000 + int v); FormatVersion = Events.formatVersion; SemanticVersion = Events.semanticVersion }
              Payload =
                CoreEvent(
                    ConclusionRecorded
                        { ConclusionId = ConclusionId(g 70); Statement = t conclusionStatement
                          Strength = ConclusionStrength.LeadingExplanation; CauseIds = []
                          ResidualUncertainty = Some(t "The certificate renewal log is incomplete") }
                ) }
        match Kernel.domain.Evolve (Some s) env with
        | Ok x -> x
        | Error e -> failwithf "the stored conclusion did not evolve: %A" e

    /// Everything above plus a causal assertion and a stored conclusion: every list the sections render is non-empty.
    let full () =
        continueWith
            (reassessed ())
            [ RecordCausalAssertion
                  { CauseId = CausalAssertionId(g 60); Outcome = t "Checkout payments fail"
                    Members = [ { HypothesisId = h1; Role = CauseRole.RootCause } ]; Mechanism = None }
              IdentifyUnknown { UnknownId = UnknownId(g 80); Question = t "When was the certificate last renewed?"; Materiality = Materiality.Material }
              RecordInvestigationAction
                  { ActionId = InvestigationActionId(g 81); Kind = ActionKind.Inspection; Purpose = t "Inspect the certificate"
                    TargetHypothesisIds = [ h1 ] }
              RecordFinding
                  { FindingId = FindingId(g 82); ActionId = InvestigationActionId(g 81); OriginalResult = t "The certificate expired at 08:59"
                    Interpretation = Some(t "matches the onset") } ]
        |> withStoredConclusion
