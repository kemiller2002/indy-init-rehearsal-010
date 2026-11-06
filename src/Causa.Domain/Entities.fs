// Owner: domain. Event payload records pinned to the causa-storage v1.0.0 wire (CONTRACT section 7: exactly the
// payload fields, nullable fields as option, arrays as lists in stored order) and the entity state the replay projects.
namespace Causa.Domain

open System

/// The envelope actor (CONTRACT section 5): `{actorId, kind}`, plus `provider` and `model` for an agent.
[<RequireQualifiedAccess>]
type ActorKind =
    | Human
    | System
    | Agent of provider: NonEmptyText * model: NonEmptyText

type Actor = { ActorId: NonEmptyText; Kind: ActorKind }

/// Wire `InvestigationStarted`: `title`, `observationId`, `original`.
type InvestigationStarted =
    { Title: NonEmptyText
      ObservationId: ObservationId
      Original: NonEmptyText }

/// Wire `ObservationRecorded`: `observationId`, `original`, `observedAt` (nullable).
type Observation =
    { ObservationId: ObservationId
      Original: NonEmptyText
      ObservedAt: DateTimeOffset option }

/// Wire `HypothesisRecorded`: `hypothesisId`, `statement`, `mechanism` (nullable).
type Hypothesis =
    { HypothesisId: HypothesisId
      Statement: NonEmptyText
      Mechanism: NonEmptyText option }

/// Wire `HypothesisTransitioned` less `to` and `command`, which the CoreEvent case determines: `hypothesisId`, `from`,
/// `reason`, `evidenceIds`.
type HypothesisTransition =
    { HypothesisId: HypothesisId
      From: HypothesisState
      Reason: NonEmptyText
      EvidenceIds: EvidenceId list }

/// Wire `EvidenceAdmitted`: `evidenceId`, `contentKind`, `content`, `category`, `source`, `capturedAt` (nullable).
type Evidence =
    { EvidenceId: EvidenceId
      ContentKind: ContentKind
      Content: NonEmptyText
      Category: EvidenceCategory
      Source: NonEmptyText
      CapturedAt: DateTimeOffset option }

/// Wire `EvidenceSuperseded`: `evidenceId`, `replacementEvidenceId`, `reason`.
type EvidenceSupersession =
    { EvidenceId: EvidenceId
      ReplacementEvidenceId: EvidenceId
      Reason: NonEmptyText }

/// Wire `EvidenceVerificationAssessed`: `evidenceId`, `verification`.
type EvidenceVerificationAssessment =
    { EvidenceId: EvidenceId
      Verification: VerificationStatus }

/// Wire `EvidenceRelated`: `relationshipId`, `evidenceId`, `hypothesisId`, `kind`, `rationale` (nullable).
type EvidenceRelationship =
    { RelationshipId: EvidenceRelationshipId
      EvidenceId: EvidenceId
      HypothesisId: HypothesisId
      Kind: RelationshipKind
      Rationale: NonEmptyText option }

/// Wire `UnknownIdentified`: `unknownId`, `question`, `materiality`.
type Unknown =
    { UnknownId: UnknownId
      Question: NonEmptyText
      Materiality: Materiality }

/// Wire `UnknownTransitioned`: `unknownId`, `state`, `basis`.
type UnknownTransition =
    { UnknownId: UnknownId
      State: UnknownState
      Basis: NonEmptyText }

/// Wire `AssumptionRecorded`: `assumptionId`, `proposition`, `materiality`.
type Assumption =
    { AssumptionId: AssumptionId
      Proposition: NonEmptyText
      Materiality: Materiality }

/// Wire `AssumptionTransitioned`: `assumptionId`, `state`.
type AssumptionTransition =
    { AssumptionId: AssumptionId
      State: AssumptionState }

/// Wire `ContradictionRecorded`: `contradictionId`, `statement`, `evidenceIds`, `materiality`.
type Contradiction =
    { ContradictionId: ContradictionId
      Statement: NonEmptyText
      EvidenceIds: EvidenceId list
      Materiality: Materiality }

/// Wire `ContradictionTransitioned`: `contradictionId`, `state`, `basis`.
type ContradictionTransition =
    { ContradictionId: ContradictionId
      State: ContradictionState
      Basis: NonEmptyText }

/// Wire `PredictionRecorded`: `predictionId`, `hypothesisId`, `statement`, `strength`, `timing`.
type Prediction =
    { PredictionId: PredictionId
      HypothesisId: HypothesisId
      Statement: NonEmptyText
      Strength: PredictionStrength
      Timing: PredictionTiming }

/// Wire `ActionRecorded`: `actionId`, `kind`, `purpose`, `targetHypothesisIds`.
type InvestigationAction =
    { ActionId: InvestigationActionId
      Kind: ActionKind
      Purpose: NonEmptyText
      TargetHypothesisIds: HypothesisId list }

/// Wire `ActionTransitioned`: `actionId`, `state`.
type InvestigationActionTransition =
    { ActionId: InvestigationActionId
      State: ActionState }

/// Wire `FindingRecorded`: `findingId`, `actionId`, `originalResult`, `interpretation` (nullable).
type Finding =
    { FindingId: FindingId
      ActionId: InvestigationActionId
      OriginalResult: NonEmptyText
      Interpretation: NonEmptyText option }

/// Wire `NegativeKnowledgeEstablished`: `negativeKnowledgeId`, `statement`, `scope` (text), `basisEvidenceIds`.
type NegativeKnowledge =
    { NegativeKnowledgeId: NegativeKnowledgeId
      Statement: NonEmptyText
      Scope: NonEmptyText
      BasisEvidenceIds: EvidenceId list }

/// A member of a causal assertion: `{hypothesisId, role}`.
type CausalMember = { HypothesisId: HypothesisId; Role: CauseRole }

/// Wire `CausalAssertionRecorded`: `causeId`, `outcome`, `members`, `mechanism` (nullable).
type CausalAssertion =
    { CauseId: CausalAssertionId
      Outcome: NonEmptyText
      Members: CausalMember list
      Mechanism: NonEmptyText option }

/// Wire `CausalAssertionConfirmed`: `causeId`, `policy`.
type CauseConfirmation =
    { CauseId: CausalAssertionId
      Policy: NonEmptyText }

/// Wire `CorrectiveActionRecorded`: `correctiveActionId`, `kind`, `description`, `targetCauseIds`.
type CorrectiveAction =
    { CorrectiveActionId: CorrectiveActionId
      Kind: CorrectiveActionKind
      Description: NonEmptyText
      TargetCauseIds: CausalAssertionId list }

/// Wire `CriterionRecorded`: `criterionId`, `correctiveActionId`, `expectedOutcome`, `method` (nullable).
type VerificationCriterion =
    { CriterionId: VerificationCriterionId
      CorrectiveActionId: CorrectiveActionId
      ExpectedOutcome: NonEmptyText
      Method: NonEmptyText option }

/// Wire `VerificationRecorded`: `verificationId`, `criterionId`, `outcome`, `evidenceIds`.
type Verification =
    { VerificationId: VerificationId
      CriterionId: VerificationCriterionId
      Outcome: VerificationOutcome
      EvidenceIds: EvidenceId list }

/// Wire `ConclusionRecorded`: `conclusionId`, `statement`, `strength`, `causeIds`, `residualUncertainty` (nullable).
type Conclusion =
    { ConclusionId: ConclusionId
      Statement: NonEmptyText
      Strength: ConclusionStrength
      CauseIds: CausalAssertionId list
      ResidualUncertainty: NonEmptyText option }

/// Wire `ProposalRecorded`: `proposalId`, `kind`, `proposalText`, `rationale` (nullable).
type Proposal =
    { ProposalId: ProposalId
      Kind: ProposalKind
      ProposalText: NonEmptyText
      Rationale: NonEmptyText option }

/// Who recorded an entity, when, and at which aggregate version (CONTRACT section 8.3); never changed by updates.
type EntityProvenance =
    { Actor: Actor
      RecordedAt: DateTimeOffset
      AtVersion: AggregateVersion }

/// An immutable recorded entity (observation, relationship, prediction, finding, negative knowledge, criterion,
/// verification, conclusion).
type Recorded<'T> = { Value: 'T; Provenance: EntityProvenance }

/// One hypothesis transition as replayed (the reason and evidence are kept; nothing is overwritten).
type HypothesisChange =
    { From: HypothesisState
      To: HypothesisState
      Reason: NonEmptyText
      EvidenceIds: EvidenceId list
      At: DateTimeOffset
      Version: AggregateVersion }

type HypothesisEntry =
    { Hypothesis: Hypothesis
      State: HypothesisState
      History: HypothesisChange list
      Provenance: EntityProvenance }

type EvidenceEntry =
    { Evidence: Evidence
      Verification: VerificationStatus
      SupersededBy: EvidenceId option
      Provenance: EntityProvenance }

type UnknownEntry =
    { Unknown: Unknown
      State: UnknownState
      Basis: NonEmptyText option
      Provenance: EntityProvenance }

type AssumptionEntry =
    { Assumption: Assumption
      State: AssumptionState
      Provenance: EntityProvenance }

type ContradictionEntry =
    { Contradiction: Contradiction
      State: ContradictionState
      Basis: NonEmptyText option
      Provenance: EntityProvenance }

type ActionEntry =
    { Action: InvestigationAction
      State: ActionState
      Provenance: EntityProvenance }

type CauseEntry =
    { Cause: CausalAssertion
      State: CauseState
      ConfirmedUnder: NonEmptyText option
      Provenance: EntityProvenance }

type CorrectiveActionEntry =
    { CorrectiveAction: CorrectiveAction
      State: CorrectiveActionState
      AwaitingSince: AggregateVersion option
      Provenance: EntityProvenance }

type ProposalEntry =
    { Proposal: Proposal
      State: ProposalState
      AcceptedAs: Guid option
      RejectionReason: NonEmptyText option
      Provenance: EntityProvenance }
