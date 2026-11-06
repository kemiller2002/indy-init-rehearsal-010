// Owner: domain. Events (frozen case names of 00-DOMAIN-STUBS.fs, v4.1.5 E1: one case for every causa-storage v1.0.0
// event) and the event envelope with the wire's provenance; the investigation aggregate the kernel replays.
namespace Causa.Domain

open System

/// Frozen (00-DOMAIN-STUBS.fs, v4.1.5 E1).
type CoreEvent =
    | InvestigationStarted of InvestigationStarted
    | InvestigationActivated of from: Lifecycle
    | InvestigationMarkedAwaitingEvidence of from: Lifecycle * reason: NonEmptyText
    | InvestigationMarkedAwaitingVerification of from: Lifecycle
    | InvestigationResolved of from: Lifecycle
    | InvestigationClosed of from: Lifecycle
    | InvestigationReopened of from: Lifecycle * reason: NonEmptyText
    | InvestigationVerificationFailed of reason: NonEmptyText
    | ObservationRecorded of Observation
    | HypothesisRecorded of Hypothesis
    | HypothesisMarkedPlausible of HypothesisTransition
    | HypothesisMarkedSupported of HypothesisTransition
    | HypothesisWeakened of HypothesisTransition
    | HypothesisFalsified of HypothesisTransition
    | HypothesisSuperseded of HypothesisTransition
    | HypothesisReopened of HypothesisTransition
    | EvidenceAdmitted of Evidence
    | EvidenceSuperseded of EvidenceSupersession
    | EvidenceVerificationAssessed of EvidenceVerificationAssessment
    | EvidenceRelationshipRecorded of EvidenceRelationship
    | UnknownIdentified of Unknown
    | UnknownTransitioned of UnknownTransition
    | AssumptionRecorded of Assumption
    | AssumptionTransitioned of AssumptionTransition
    | ContradictionRecorded of Contradiction
    | ContradictionTransitioned of ContradictionTransition
    | PredictionRecorded of Prediction
    | InvestigationActionRecorded of InvestigationAction
    | InvestigationActionTransitioned of InvestigationActionTransition
    | FindingRecorded of Finding
    | NegativeKnowledgeEstablished of NegativeKnowledge
    | CausalAssertionRecorded of CausalAssertion
    | CauseConfirmed of CauseConfirmation
    | ConfirmedCauseReopened of CausalAssertionId * reason: NonEmptyText
    | CorrectiveActionRecorded of CorrectiveAction
    | CorrectiveActionApproved of CorrectiveActionId
    | CorrectiveActionStarted of CorrectiveActionId
    | CorrectiveActionImplemented of CorrectiveActionId
    | CorrectiveActionAwaitingVerificationOfEffect of CorrectiveActionId
    | CorrectiveActionVerifiedEffective of CorrectiveActionId
    | CorrectiveActionVerificationFailed of CorrectiveActionId
    | CorrectiveActionReworkRequired of CorrectiveActionId
    | CorrectiveActionCancelled of CorrectiveActionId
    | VerificationCriterionRecorded of VerificationCriterion
    | VerificationRecorded of Verification
    | ConclusionRecorded of Conclusion
    | ProposalRecorded of Proposal
    | ProposalAccepted of ProposalId * targetId: Guid
    | ProposalRejected of ProposalId * reason: NonEmptyText
    | ExtensionAttached of ExtensionEnvelope

/// Frozen.
type EventPayload =
    | CoreEvent of CoreEvent
    | ExtensionEvent of SemanticId * payloadJson: string

/// Frozen name, v4.1.5 E1: exactly the stored envelope's provenance fields (`occurredAt`, `actor`, `correlationId`,
/// `formatVersion`, `semanticVersion`). occurredAt is a UTC instant at millisecond precision (CONTRACT section 2.9).
type EventProvenance =
    { OccurredAt: DateTimeOffset
      Actor: Actor
      CorrelationId: Guid
      FormatVersion: string
      SemanticVersion: string }

/// Frozen.
type EventEnvelope =
    { EventId: EventId
      Investigation: InvestigationId
      Version: AggregateVersion
      Provenance: EventProvenance
      Payload: EventPayload }

/// The investigation aggregate (FSHARP-TYPE-SHAPES section 30, narrowed to what causa-storage v1.0.0 stores; the
/// CONTRACT section 8.2 state). `Id` and `Version` are the frozen fields. Maps are keyed by typed id, whose order is the
/// UUID order. `History` is the applied envelopes, newest first; `Correlations` maps each command (correlation id) to
/// the events it produced, so a repeated command is recognised as a duplicate.
type Investigation =
    { Id: InvestigationId
      Version: AggregateVersion
      Title: NonEmptyText
      Lifecycle: Lifecycle
      CreatedAt: DateTimeOffset
      UpdatedAt: DateTimeOffset
      Observations: Map<ObservationId, Recorded<Observation>>
      Hypotheses: Map<HypothesisId, HypothesisEntry>
      Evidence: Map<EvidenceId, EvidenceEntry>
      Relationships: Map<EvidenceRelationshipId, Recorded<EvidenceRelationship>>
      Unknowns: Map<UnknownId, UnknownEntry>
      Assumptions: Map<AssumptionId, AssumptionEntry>
      Contradictions: Map<ContradictionId, ContradictionEntry>
      Predictions: Map<PredictionId, Recorded<Prediction>>
      Actions: Map<InvestigationActionId, ActionEntry>
      Findings: Map<FindingId, Recorded<Finding>>
      NegativeKnowledge: Map<NegativeKnowledgeId, Recorded<NegativeKnowledge>>
      Causes: Map<CausalAssertionId, CauseEntry>
      CorrectiveActions: Map<CorrectiveActionId, CorrectiveActionEntry>
      Criteria: Map<VerificationCriterionId, Recorded<VerificationCriterion>>
      Verifications: Map<VerificationId, Recorded<Verification>>
      Conclusions: Map<ConclusionId, Recorded<Conclusion>>
      Proposals: Map<ProposalId, ProposalEntry>
      Correlations: Map<Guid, EventId list>
      History: EventEnvelope list }

[<RequireQualifiedAccess>]
module Events =
    /// The causa-storage v1.0.0 format and semantic version every stored envelope carries (CONTRACT sections 4-5).
    let formatVersion = "1.0.0"
    let semanticVersion = "1.0.0"

    /// The case name of an event (reflection-free).
    let caseName (e: CoreEvent) : string =
        match e with
        | InvestigationStarted _ -> "InvestigationStarted"
        | InvestigationActivated _ -> "InvestigationActivated"
        | InvestigationMarkedAwaitingEvidence _ -> "InvestigationMarkedAwaitingEvidence"
        | InvestigationMarkedAwaitingVerification _ -> "InvestigationMarkedAwaitingVerification"
        | InvestigationResolved _ -> "InvestigationResolved"
        | InvestigationClosed _ -> "InvestigationClosed"
        | InvestigationReopened _ -> "InvestigationReopened"
        | InvestigationVerificationFailed _ -> "InvestigationVerificationFailed"
        | ObservationRecorded _ -> "ObservationRecorded"
        | HypothesisRecorded _ -> "HypothesisRecorded"
        | HypothesisMarkedPlausible _ -> "HypothesisMarkedPlausible"
        | HypothesisMarkedSupported _ -> "HypothesisMarkedSupported"
        | HypothesisWeakened _ -> "HypothesisWeakened"
        | HypothesisFalsified _ -> "HypothesisFalsified"
        | HypothesisSuperseded _ -> "HypothesisSuperseded"
        | HypothesisReopened _ -> "HypothesisReopened"
        | EvidenceAdmitted _ -> "EvidenceAdmitted"
        | EvidenceSuperseded _ -> "EvidenceSuperseded"
        | EvidenceVerificationAssessed _ -> "EvidenceVerificationAssessed"
        | EvidenceRelationshipRecorded _ -> "EvidenceRelationshipRecorded"
        | UnknownIdentified _ -> "UnknownIdentified"
        | UnknownTransitioned _ -> "UnknownTransitioned"
        | AssumptionRecorded _ -> "AssumptionRecorded"
        | AssumptionTransitioned _ -> "AssumptionTransitioned"
        | ContradictionRecorded _ -> "ContradictionRecorded"
        | ContradictionTransitioned _ -> "ContradictionTransitioned"
        | PredictionRecorded _ -> "PredictionRecorded"
        | InvestigationActionRecorded _ -> "InvestigationActionRecorded"
        | InvestigationActionTransitioned _ -> "InvestigationActionTransitioned"
        | FindingRecorded _ -> "FindingRecorded"
        | NegativeKnowledgeEstablished _ -> "NegativeKnowledgeEstablished"
        | CausalAssertionRecorded _ -> "CausalAssertionRecorded"
        | CauseConfirmed _ -> "CauseConfirmed"
        | ConfirmedCauseReopened _ -> "ConfirmedCauseReopened"
        | CorrectiveActionRecorded _ -> "CorrectiveActionRecorded"
        | CorrectiveActionApproved _ -> "CorrectiveActionApproved"
        | CorrectiveActionStarted _ -> "CorrectiveActionStarted"
        | CorrectiveActionImplemented _ -> "CorrectiveActionImplemented"
        | CorrectiveActionAwaitingVerificationOfEffect _ -> "CorrectiveActionAwaitingVerificationOfEffect"
        | CorrectiveActionVerifiedEffective _ -> "CorrectiveActionVerifiedEffective"
        | CorrectiveActionVerificationFailed _ -> "CorrectiveActionVerificationFailed"
        | CorrectiveActionReworkRequired _ -> "CorrectiveActionReworkRequired"
        | CorrectiveActionCancelled _ -> "CorrectiveActionCancelled"
        | VerificationCriterionRecorded _ -> "VerificationCriterionRecorded"
        | VerificationRecorded _ -> "VerificationRecorded"
        | ConclusionRecorded _ -> "ConclusionRecorded"
        | ProposalRecorded _ -> "ProposalRecorded"
        | ProposalAccepted _ -> "ProposalAccepted"
        | ProposalRejected _ -> "ProposalRejected"
        | ExtensionAttached _ -> "ExtensionAttached"

    /// The lifecycle an investigation lifecycle event moves to (wire `LifecycleChanged.to`), with the event's `from`.
    /// None for events that are not lifecycle changes.
    let lifecycleChange (e: CoreEvent) : (Lifecycle * Lifecycle) option =
        match e with
        | InvestigationActivated f -> Some(f, Lifecycle.Active)
        | InvestigationMarkedAwaitingEvidence(f, _) -> Some(f, Lifecycle.AwaitingEvidence)
        | InvestigationMarkedAwaitingVerification f -> Some(f, Lifecycle.AwaitingVerification)
        | InvestigationResolved f -> Some(f, Lifecycle.Resolved)
        | InvestigationClosed f -> Some(f, Lifecycle.Closed)
        | InvestigationReopened(f, _) -> Some(f, Lifecycle.Active)
        | InvestigationVerificationFailed _ -> Some(Lifecycle.AwaitingVerification, Lifecycle.Active)
        | _ -> None

    /// The hypothesis transition of an event and the state it moves to (wire `HypothesisTransitioned.to`).
    let hypothesisChange (e: CoreEvent) : (HypothesisTransition * HypothesisState) option =
        match e with
        | HypothesisMarkedPlausible t -> Some(t, HypothesisState.Plausible)
        | HypothesisMarkedSupported t -> Some(t, HypothesisState.Supported)
        | HypothesisWeakened t -> Some(t, HypothesisState.Weakened)
        | HypothesisFalsified t -> Some(t, HypothesisState.Falsified)
        | HypothesisSuperseded t -> Some(t, HypothesisState.Superseded)
        | HypothesisReopened t -> Some(t, HypothesisState.Plausible)
        | _ -> None

    /// The corrective action transition of an event and the state it moves to (wire `CorrectiveActionTransitioned`).
    let correctiveActionChange (e: CoreEvent) : (CorrectiveActionId * CorrectiveActionState) option =
        match e with
        | CorrectiveActionApproved id -> Some(id, CorrectiveActionState.Approved)
        | CorrectiveActionStarted id -> Some(id, CorrectiveActionState.InProgress)
        | CorrectiveActionImplemented id -> Some(id, CorrectiveActionState.Implemented)
        | CorrectiveActionAwaitingVerificationOfEffect id -> Some(id, CorrectiveActionState.AwaitingVerificationOfEffect)
        | CorrectiveActionVerifiedEffective id -> Some(id, CorrectiveActionState.VerifiedEffective)
        | CorrectiveActionVerificationFailed id -> Some(id, CorrectiveActionState.VerificationFailed)
        | CorrectiveActionReworkRequired id -> Some(id, CorrectiveActionState.ReworkRequired)
        | CorrectiveActionCancelled id -> Some(id, CorrectiveActionState.Cancelled)
        | _ -> None
