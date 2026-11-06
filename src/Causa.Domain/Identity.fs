// Owner: domain. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
// The domain stubs of the interface contracts; the domain owner implements the full shapes.
namespace Causa.Domain

open System

/// Abbreviated from docs/FSHARP-TYPE-SHAPES.md (sections 2, 30-38) for the contract typecheck only. The domain owner
/// implements the full shapes; these stubs fix only the names the boundaries use.
type InvestigationId = InvestigationId of Guid
type EventId = EventId of Guid
type AggregateVersion = AggregateVersion of int64
type Investigation = { Id: InvestigationId; Version: AggregateVersion }
/// v4.1.2 (ODR-V412-02): the command and event bodies of FSHARP-TYPE-SHAPES sections 31 and 33. The case names are
/// frozen here so every consumer can tell commands and events apart; each payload type is the domain owner's to
/// define (opaque in this stub, as the other stubs abbreviate their shapes).
type SemanticId = class end
type ExtensionEnvelope = class end
type StartInvestigation = class end
type RecordObservation = class end
type RecordHypothesis = class end
type FalsifyHypothesis = class end
type ReopenHypothesis = class end
type AdmitEvidence = class end
type RelateEvidence = class end
type IdentifyUnknown = class end
type RecordAssumption = class end
type RecordContradiction = class end
type RecordPrediction = class end
type RecordInvestigationAction = class end
type RecordFinding = class end
type EstablishNegativeKnowledge = class end
type RecordCausalAssertion = class end
type ConfirmCause = class end
type RecordCorrectiveAction = class end
type RecordVerificationCriterion = class end
type RecordVerification = class end
type ReopenInvestigation = class end
type RecordProposal = class end
type InvestigationStarted = class end
type Observation = class end
type Hypothesis = class end
type NonEmptyText = class end
type Scope = class end
type Evidence = class end
type EvidenceRelationship = class end
type Unknown = class end
type Assumption = class end
type Contradiction = class end
type Prediction = class end
type InvestigationAction = class end
type Finding = class end
type CausalAssertion = class end
type CorrectiveAction = class end
type VerificationCriterion = class end
type Verification = class end
type Proposal = class end
type HypothesisId = HypothesisId of Guid
type CorrectiveActionId = CorrectiveActionId of Guid
type ProposalId = ProposalId of Guid
type CausalAssertionId = CausalAssertionId of Guid
/// v4.1.5 E1: the commands whose events now carry the wire's fields carry them too (reason; evidence ids), opaque
/// like the other command payloads, so Decide can produce events that encode.
type MarkHypothesis = class end
type CoreCommand =
    | StartInvestigation of StartInvestigation
    | ActivateInvestigation
    | RecordObservation of RecordObservation
    | RecordHypothesis of RecordHypothesis
    | MarkHypothesisPlausible of MarkHypothesis
    | MarkHypothesisSupported of MarkHypothesis
    | FalsifyHypothesis of FalsifyHypothesis
    | ReopenHypothesis of ReopenHypothesis
    | AdmitEvidence of AdmitEvidence
    | RelateEvidence of RelateEvidence
    | IdentifyUnknown of IdentifyUnknown
    | RecordAssumption of RecordAssumption
    | RecordContradiction of RecordContradiction
    | RecordPrediction of RecordPrediction
    | RecordInvestigationAction of RecordInvestigationAction
    | RecordFinding of RecordFinding
    | EstablishNegativeKnowledge of EstablishNegativeKnowledge
    | RecordCausalAssertion of RecordCausalAssertion
    | ConfirmCause of ConfirmCause
    | RecordCorrectiveAction of RecordCorrectiveAction
    | RecordVerificationCriterion of RecordVerificationCriterion
    | MarkCorrectiveActionImplemented of CorrectiveActionId
    | RecordVerification of RecordVerification
    | ResolveInvestigation
    | ReopenInvestigation of ReopenInvestigation
    | RecordProposal of RecordProposal
    | AcceptProposal of ProposalId
    | RejectProposal of ProposalId * reason: NonEmptyText
    | AttachExtension of ExtensionEnvelope
type Command =
    | Core of CoreCommand
    | Extension of SemanticId * payloadJson: string
/// v4.1.5 E1 (historical compatibility, OD-HC-01 A; ODR-R10-25 A): one case for every causa-storage v1.0.0 event
/// (CONTRACT.md section 7), so every catalog history is representable without loss. Every v4.1.4 case name is kept;
/// nine cases carry the wire's extra fields; 22 cases are added; HypothesisFalsified no longer carries a Scope the
/// wire does not have. Payload types stay the domain owner's to define (opaque here, as in v4.1.2), with exactly
/// the fields of their wire records: encoding then decoding any stored event must reproduce its canonical bytes.
type Lifecycle = class end
type HypothesisTransition = class end
type EvidenceSupersession = class end
type EvidenceVerificationAssessment = class end
type UnknownTransition = class end
type AssumptionTransition = class end
type ContradictionTransition = class end
type InvestigationActionTransition = class end
type NegativeKnowledge = class end
type CauseConfirmation = class end
type Conclusion = class end
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
type CommandEnvelope = { CommandId: Guid; Investigation: InvestigationId; ExpectedVersion: AggregateVersion option; Command: Command }
type EventPayload =
    | CoreEvent of CoreEvent
    | ExtensionEvent of SemanticId * payloadJson: string
/// v4.1.5 E1: the stored envelope's provenance (occurredAt, actor, correlationId, formatVersion, semanticVersion),
/// opaque here and the domain owner's to define with exactly the wire fields.
type EventProvenance = class end
type EventEnvelope = { EventId: EventId; Investigation: InvestigationId; Version: AggregateVersion; Provenance: EventProvenance; Payload: EventPayload }
type Blocker = { Code: string; Message: string }
type Decision =
    | Allowed of EventPayload list
    | Blocked of Blocker list
    | RequiresReview of string list
    | UnsupportedSemantics of string list
    | Conflict of expected: AggregateVersion * actual: AggregateVersion
    | Duplicate of EventId list
type PolicySet = { Id: string }
type ActorContext = { ActorId: string }
type ProjectionError = { At: AggregateVersion; Reason: string }
