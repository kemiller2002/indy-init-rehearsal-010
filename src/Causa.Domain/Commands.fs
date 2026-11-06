// Owner: domain. Commands (FSHARP-TYPE-SHAPES sections 31-32; frozen case names of 00-DOMAIN-STUBS.fs). A command
// carries every identity and value its events need, supplied by the command-creation layer (section 43), so Decide
// stays pure: it never invents an id, reads a clock or draws a random number.
namespace Causa.Domain

open System

/// Start a new investigation: its title and the original problem statement, recorded as its first observation.
type StartInvestigation = InvestigationStarted
type RecordObservation = Observation
type RecordHypothesis = Hypothesis

/// v4.1.5 E1: a hypothesis transition carries the wire's reason and evidence ids.
type MarkHypothesis =
    { HypothesisId: HypothesisId
      Reason: NonEmptyText
      EvidenceIds: EvidenceId list }

type FalsifyHypothesis = MarkHypothesis
type ReopenHypothesis = MarkHypothesis
type AdmitEvidence = Evidence
type RelateEvidence = EvidenceRelationship
type IdentifyUnknown = Unknown
type RecordAssumption = Assumption
type RecordContradiction = Contradiction
type RecordPrediction = Prediction
type RecordInvestigationAction = InvestigationAction
type RecordFinding = Finding
type EstablishNegativeKnowledge = NegativeKnowledge
type RecordCausalAssertion = CausalAssertion
type ConfirmCause = CauseConfirmation
type RecordCorrectiveAction = CorrectiveAction
type RecordVerificationCriterion = VerificationCriterion
type RecordVerification = Verification
type ReopenInvestigation = { Reason: NonEmptyText }
type RecordProposal = Proposal

/// Frozen (00-DOMAIN-STUBS.fs).
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

/// Frozen.
type Command =
    | Core of CoreCommand
    | Extension of SemanticId * payloadJson: string

/// Frozen. CommandId becomes the stored events' correlationId (one command, one correlation).
type CommandEnvelope =
    { CommandId: Guid
      Investigation: InvestigationId
      ExpectedVersion: AggregateVersion option
      Command: Command }

[<RequireQualifiedAccess>]
module Commands =
    /// The case name of a command, for capabilities, blockers and diagnostics (reflection-free).
    let name (c: CoreCommand) : string =
        match c with
        | StartInvestigation _ -> "StartInvestigation"
        | ActivateInvestigation -> "ActivateInvestigation"
        | RecordObservation _ -> "RecordObservation"
        | RecordHypothesis _ -> "RecordHypothesis"
        | MarkHypothesisPlausible _ -> "MarkHypothesisPlausible"
        | MarkHypothesisSupported _ -> "MarkHypothesisSupported"
        | FalsifyHypothesis _ -> "FalsifyHypothesis"
        | ReopenHypothesis _ -> "ReopenHypothesis"
        | AdmitEvidence _ -> "AdmitEvidence"
        | RelateEvidence _ -> "RelateEvidence"
        | IdentifyUnknown _ -> "IdentifyUnknown"
        | RecordAssumption _ -> "RecordAssumption"
        | RecordContradiction _ -> "RecordContradiction"
        | RecordPrediction _ -> "RecordPrediction"
        | RecordInvestigationAction _ -> "RecordInvestigationAction"
        | RecordFinding _ -> "RecordFinding"
        | EstablishNegativeKnowledge _ -> "EstablishNegativeKnowledge"
        | RecordCausalAssertion _ -> "RecordCausalAssertion"
        | ConfirmCause _ -> "ConfirmCause"
        | RecordCorrectiveAction _ -> "RecordCorrectiveAction"
        | RecordVerificationCriterion _ -> "RecordVerificationCriterion"
        | MarkCorrectiveActionImplemented _ -> "MarkCorrectiveActionImplemented"
        | RecordVerification _ -> "RecordVerification"
        | ResolveInvestigation -> "ResolveInvestigation"
        | ReopenInvestigation _ -> "ReopenInvestigation"
        | RecordProposal _ -> "RecordProposal"
        | AcceptProposal _ -> "AcceptProposal"
        | RejectProposal _ -> "RejectProposal"
        | AttachExtension _ -> "AttachExtension"
