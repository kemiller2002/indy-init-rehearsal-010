# Command and Event Catalog

Status: normative first implementation profile.

This document defines the initial authoritative command and event vocabulary for the Root-Cause Investigator. It is intentionally more concrete than the semantic specifications, but it preserves the research boundaries in OPEN-QUESTIONS.md.

The catalog follows one rule:

> Persist what happened and what a human/system explicitly decided. Derive comparative or analytical posture where practical.

In particular, the first implementation should avoid persisting "Leading" merely because it is currently displayed as a hypothesis posture. Leading is initially a derived comparison unless later research promotes it to durable state.

## 1. Command conventions

Every command is carried inside CommandEnvelope from POLICY-ENGINE.md.

A command:

- expresses intent
- names its target explicitly
- carries ExpectedAggregateVersion
- carries Actor and provenance
- is evaluated by the policy engine
- emits zero or more semantic events when allowed
- never writes persistence directly

Commands are named as verbs.

Events are named in past tense.

## 2. Event conventions

Events record completed authoritative facts.

Events must not contain UI-only state.

Events should be narrow enough that later projections can evolve without rewriting history.

Bad event:

```text
WorkspaceUpdated
```

Good events:

```text
EvidenceAdmitted
HypothesisFalsified
UnknownAcceptedAsResidual
```

## 3. Investigation commands

| Command | Primary event(s) | Core prerequisites |
| --- | --- | --- |
| StartInvestigation | InvestigationStarted | valid identity, title/source statement, compatible semantic manifest |
| RenameInvestigation | InvestigationRenamed | investigation exists, actor authorized |
| ActivateInvestigation | InvestigationActivated | minimum activation policy satisfied |
| MarkAwaitingEvidence | InvestigationMarkedAwaitingEvidence | active investigation, reason supplied |
| MarkAwaitingVerification | InvestigationMarkedAwaitingVerification | verification work exists |
| ResolveInvestigation | InvestigationResolved | resolution gate satisfied |
| CloseInvestigation | InvestigationClosed | resolution/closure policy satisfied |
| ReopenInvestigation | InvestigationReopened | new evidence, failed verification, contradiction, or explicit policy-approved basis |
| ArchiveInvestigation | InvestigationArchived | archive policy satisfied |
| RelateInvestigation | InvestigationRelationshipAdded | both references valid/authorized |
| RemoveInvestigationRelationship | InvestigationRelationshipSuperseded | relationship exists; history retained |

Notes:

- Draft is represented by InvestigationStarted before activation.
- Reopened is better represented by an event and lifecycle transition back to Active than as a permanent standalone lifecycle state.
- Archived is administrative and must not erase semantic history.

## 4. Observation commands

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordObservation | ObservationRecorded | original source content present |
| AddObservationNormalization | ObservationNormalizationAdded | observation exists |
| VerifyObservation | ObservationVerificationChanged | verification basis supplied |
| SupersedeObservation | ObservationSuperseded | replacement/basis supplied |

Invariant:

Observation original content is immutable after ObservationRecorded. Corrections use supersession or added normalization, not rewriting.

## 5. Claim commands

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordClaim | ClaimRecorded | proposition and scope valid |
| AddClaimAssessment | ClaimAssessmentAdded | claim exists; assessment basis supplied |
| SupersedeClaim | ClaimSuperseded | replacement/reason supplied |

Claim epistemic posture should initially be derived from assessments/evidence relationships rather than changed through a generic SetClaimState command.

## 6. Interpretation commands

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordInterpretation | InterpretationRecorded | source refs or explicit unsupported-proposal marker |
| SupersedeInterpretation | InterpretationSuperseded | interpretation exists; reason supplied |

Interpretations do not become Evidence merely by being recorded.

## 7. Hypothesis commands

Authoritative hypothesis lifecycle for the first implementation:

```text
Proposed
  -> Plausible
  -> Supported
  -> Confirmed

Proposed/Plausible/Supported
  -> Weakened
  -> Falsified

Any non-confirmed active state
  -> Superseded

Falsified
  -> Plausible via ReopenHypothesis

Confirmed
  -> Reopened/Supported or other policy-defined active posture
```

"Leading" is initially derived rather than persisted.

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordHypothesis | HypothesisRecorded | testable explanatory proposition |
| MarkHypothesisPlausible | HypothesisMarkedPlausible | relevance/rationale, no known immediate falsifier |
| MarkHypothesisSupported | HypothesisMarkedSupported | material support, no policy blocker |
| MarkHypothesisWeakened | HypothesisMarkedWeakened | weakening basis |
| FalsifyHypothesis | HypothesisFalsified, possibly NegativeKnowledgeEstablished | falsifying basis + scope |
| SupersedeHypothesis | HypothesisSuperseded | reason/replacement optional |
| ReopenHypothesis | HypothesisReopened | new/corrected evidence, scope, or invalidated falsification premise |
| ConfirmCause | CauseConfirmed | causal assertion references supported hypothesis/conditions and cause-confirmation gate is satisfied |

Do not provide a generic SetHypothesisState command.

## 8. Assumption commands

Lifecycle:

```text
ExplicitUnverified
  -> ProvisionallyAccepted
  -> Verified

ExplicitUnverified/ProvisionallyAccepted
  -> Rejected

Any active state
  -> Expired
```

| Command | Event(s) |
| --- | --- |
| RecordAssumption | AssumptionRecorded |
| ProvisionallyAcceptAssumption | AssumptionProvisionallyAccepted |
| VerifyAssumption | AssumptionVerified |
| RejectAssumption | AssumptionRejected |
| ExpireAssumption | AssumptionExpired |
| RelateAssumptionDependency | AssumptionDependencyAdded |
| SupersedeAssumptionDependency | AssumptionDependencySuperseded |

A rejected/expired material assumption may emit or derive ReevaluationRequired obligations for dependent entities.

## 9. Evidence commands

Evidence admission lifecycle is intentionally small.

| Command | Event(s) | Notes |
| --- | --- | --- |
| AdmitEvidence | EvidenceAdmitted | preserves source/provenance |
| VerifyEvidence | EvidenceVerificationChanged | verification basis recorded |
| CorrectEvidenceMetadata | EvidenceMetadataCorrected | does not rewrite original source |
| SupersedeEvidence | EvidenceSuperseded | history preserved |
| MarkEvidenceArtifactUnavailable | EvidenceArtifactAvailabilityChanged | evidence record remains |
| RecordEvidenceIntegrityMismatch | EvidenceArtifactIntegrityMismatchRecorded | does not substitute new bytes |
| AddEvidenceDependency | EvidenceDependencyAdded | source dependency explicit |
| SupersedeEvidenceDependency | EvidenceDependencySuperseded | history retained |

Evidence is not physically deleted through ordinary investigation commands.

## 10. Evidence relationship commands

| Command | Event(s) |
| --- | --- |
| RelateEvidence | EvidenceRelationshipRecorded |
| ReassessEvidenceRelationship | EvidenceRelationshipReassessed |
| SupersedeEvidenceRelationship | EvidenceRelationshipSuperseded |

Relationship kinds use CoreRelationship or versioned extension SemanticId.

A Reassess event records the new assessment and reason rather than rewriting the old relationship event.

## 11. Prediction commands

| Command | Event(s) |
| --- | --- |
| RecordPrediction | PredictionRecorded |
| MarkPredictionRetrospective | PredictionMarkedRetrospective |
| SupersedePrediction | PredictionSuperseded |

Whether a prediction is prospective should normally be established from event ordering/time and result availability, not editable after the fact without an explicit corrective event.

## 12. Investigation action commands

Lifecycle:

```text
Proposed
  -> Approved
  -> InProgress
  -> Completed
  -> Interpreted

Proposed/Approved -> Rejected
Approved/InProgress -> Cancelled
Approved/InProgress -> Blocked
Blocked -> Approved/InProgress
```

| Command | Event(s) |
| --- | --- |
| RecordInvestigationAction | InvestigationActionRecorded |
| ApproveInvestigationAction | InvestigationActionApproved |
| RejectInvestigationAction | InvestigationActionRejected |
| StartInvestigationAction | InvestigationActionStarted |
| BlockInvestigationAction | InvestigationActionBlocked |
| UnblockInvestigationAction | InvestigationActionUnblocked |
| CompleteInvestigationAction | InvestigationActionCompleted |
| InterpretInvestigationAction | InvestigationActionInterpreted |
| CancelInvestigationAction | InvestigationActionCancelled |

Completed means execution ended, not that the finding supports any conclusion.

## 13. Finding commands

| Command | Event(s) |
| --- | --- |
| RecordFinding | FindingRecorded |
| NormalizeFinding | FindingNormalizationAdded |
| AdmitFindingAsEvidence | EvidenceAdmitted, FindingAdmittedAsEvidence |
| SupersedeFinding | FindingSuperseded |

Admission as evidence creates or links a distinct Evidence identity.

## 14. Unknown commands

Lifecycle:

```text
Identified
  -> Investigating
  -> Resolved

Identified/Investigating
  -> AcceptedResidual

Identified/Investigating
  -> Superseded
```

| Command | Event(s) |
| --- | --- |
| IdentifyUnknown | UnknownIdentified |
| StartInvestigatingUnknown | UnknownInvestigationStarted |
| ResolveUnknown | UnknownResolved |
| AcceptUnknownAsResidual | UnknownAcceptedAsResidual |
| SupersedeUnknown | UnknownSuperseded |
| ChangeUnknownMateriality | UnknownMaterialityChanged |

ChangeUnknownMateriality requires policy/provenance and must never be a silent convenience transition.

## 15. Negative knowledge commands

| Command | Event(s) |
| --- | --- |
| EstablishNegativeKnowledge | NegativeKnowledgeEstablished |
| ChallengeNegativeKnowledge | NegativeKnowledgeChallenged |
| SupersedeNegativeKnowledge | NegativeKnowledgeSuperseded |

Negative knowledge remains scoped. Challenge does not erase the original basis.

## 16. Contradiction commands

Lifecycle:

```text
Detected
  -> Triaged
  -> Investigating
  -> Resolved

Detected/Triaged/Investigating
  -> AcceptedResidual

Any active state
  -> Superseded
```

| Command | Event(s) |
| --- | --- |
| RecordContradiction | ContradictionRecorded |
| TriageContradiction | ContradictionTriaged |
| StartInvestigatingContradiction | ContradictionInvestigationStarted |
| ResolveContradiction | ContradictionResolved |
| AcceptContradictionAsResidual | ContradictionAcceptedAsResidual |
| SupersedeContradiction | ContradictionSuperseded |
| ChangeContradictionMateriality | ContradictionMaterialityChanged |

Resolution must identify a resolution basis, not merely a preferred side.

## 17. Obligation commands

Some obligations are derived. Durable obligations use:

| Command | Event(s) |
| --- | --- |
| AcceptObligation | ObligationAccepted |
| AssignObligation | ObligationAssigned |
| StartObligation | ObligationStarted |
| ResolveObligation | ObligationResolved |
| WaiveObligation | ObligationWaived |
| SupersedeObligation | ObligationSuperseded |

WaiveObligation exists only where originating policy explicitly permits waiver. Core invariant obligations are not waivable.

## 18. Causal assertion commands

A causal assertion has its own persisted lifecycle: Proposed, Confirmed, Reopened, Superseded. This is deliberately separate from HypothesisState.

| Command | Event(s) |
| --- | --- |
| RecordCausalAssertion | CausalAssertionRecorded |
| AddCausalMember | CausalMemberAdded |
| SupersedeCausalMember | CausalMemberSuperseded |
| RecordCausalMechanism | CausalMechanismRecorded |
| RecordCausalSupportAssessment | CausalSupportAssessmentRecorded |
| ConfirmCause | CauseConfirmed |
| ReopenConfirmedCause | ConfirmedCauseReopened |

ConfirmCause is protected by the active confirmation policy.

Causal roles use stable generic cases plus extension SemanticId.

## 19. Corrective action commands

Lifecycle:

```text
Proposed
  -> Approved
  -> InProgress
  -> Implemented
  -> AwaitingVerification
  -> VerifiedEffective

AwaitingVerification
  -> VerificationFailed
  -> ReworkRequired
```

| Command | Event(s) |
| --- | --- |
| RecordCorrectiveAction | CorrectiveActionRecorded |
| ApproveCorrectiveAction | CorrectiveActionApproved |
| StartCorrectiveAction | CorrectiveActionStarted |
| MarkCorrectiveActionImplemented | CorrectiveActionImplemented |
| StartCorrectiveActionVerification | CorrectiveActionAwaitingVerification |
| MarkCorrectiveActionVerified | CorrectiveActionVerifiedEffective |
| MarkCorrectiveActionVerificationFailed | CorrectiveActionVerificationFailed, possibly InvestigationReopened |
| RequireCorrectiveActionRework | CorrectiveActionReworkRequired |
| CancelCorrectiveAction | CorrectiveActionCancelled |

## 20. Verification commands

| Command | Event(s) |
| --- | --- |
| RecordVerificationCriterion | VerificationCriterionRecorded |
| RecordVerification | VerificationRecorded |
| SupersedeVerificationCriterion | VerificationCriterionSuperseded |
| SupersedeVerification | VerificationSuperseded |

A verification result references its criterion and evidence/finding basis.

## 21. Conclusion commands

Conclusions are authoritative snapshots of justified posture.

| Command | Event(s) |
| --- | --- |
| RecordConclusion | ConclusionRecorded |
| ReassessConclusion | ConclusionReassessed |
| SupersedeConclusion | ConclusionSuperseded |

RecordConclusion cannot grant a stronger causal state than policy permits. It summarizes authoritative state rather than overriding it.

## 22. Proposal commands

AI/human proposals can be durably recorded without becoming accepted domain state.

| Command | Event(s) |
| --- | --- |
| RecordProposal | ProposalRecorded |
| AcceptProposal | ProposalAccepted plus target domain event(s) |
| RejectProposal | ProposalRejected |
| EditProposal | ProposalRevised |
| DeferProposal | ProposalDeferred |
| MarkProposalStale | ProposalMarkedStale |

AcceptProposal re-evaluates the resulting target command at acceptance time.

It does not replay the proposal's original legality judgment.

## 23. Extension and semantic-version commands

| Command | Event(s) |
| --- | --- |
| AttachExtension | ExtensionAttached |
| SupersedeExtension | ExtensionSuperseded |
| MigrateExtensionSideBySide | ExtensionMigrationRecorded |
| ActivateDomainPack | DomainPackActivated |
| UpgradeDomainPack | DomainPackUpgraded |
| DeactivateOptionalDomainPack | DomainPackDeactivated |
| ActivateExperimentalProfile | ExperimentalProfileActivated |
| DeactivateExperimentalProfile | ExperimentalProfileDeactivated |
| PinPolicyVersion | PolicyVersionPinned |
| ChangePolicyPin | PolicyVersionPinChanged |

These commands must obey SEMANTIC-EVOLUTION.md and DOMAIN-PACK-CONTRACT.md.

## 24. Review commands

| Command | Event(s) |
| --- | --- |
| RequestReview | ReviewRequested |
| ApproveReview | ReviewApproved |
| RejectReview | ReviewRejected |
| WithdrawReview | ReviewWithdrawn |
| ExpireReview | ReviewExpired |

A review decision never substitutes for domain evidence unless domain policy explicitly defines it as a governance prerequisite rather than epistemic evidence.

## 25. Report commands

Issued reports are outputs tied to a source snapshot.

| Command | Event(s) |
| --- | --- |
| IssueInterimReport | ReportIssued |
| IssueFinalReport | ReportIssued |
| SupersedeReport | ReportSuperseded |

ReportIssued records source InvestigationId/AggregateVersion and rendering/template versions.

## 26. Import/recovery commands

These may be service/application-layer orchestration commands rather than aggregate commands, but semantic effects require explicit events.

| Command | Event(s) |
| --- | --- |
| ImportInvestigation | InvestigationImported / normal reconstructed history per import contract |
| ForkInvestigation | InvestigationForked |
| RecordRecovery | InvestigationRecoveryRecorded |
| RecordRedaction | RedactionRecorded |

Import must not fabricate historical commands/events absent from the source package.

## 27. Event categories

Core event categories:

### Identity/lifecycle
- InvestigationStarted
- InvestigationActivated
- InvestigationResolved
- InvestigationClosed
- InvestigationReopened
- InvestigationArchived
- InvestigationRenamed

### Epistemic records
- ObservationRecorded
- ClaimRecorded
- InterpretationRecorded
- HypothesisRecorded
- AssumptionRecorded
- EvidenceAdmitted
- PredictionRecorded
- FindingRecorded
- UnknownIdentified
- NegativeKnowledgeEstablished
- ContradictionRecorded

### Relationships
- EvidenceRelationshipRecorded
- EvidenceDependencyAdded
- AssumptionDependencyAdded
- InvestigationRelationshipAdded
- CausalMemberAdded

### Work/governance
- ObligationAccepted
- ReviewRequested
- ReviewApproved
- InvestigationActionRecorded
- CorrectiveActionRecorded
- VerificationCriterionRecorded

### State transitions
- HypothesisMarkedPlausible
- HypothesisMarkedSupported
- HypothesisMarkedWeakened
- HypothesisFalsified
- HypothesisReopened
- CauseConfirmed
- UnknownResolved
- ContradictionResolved
- CorrectiveActionImplemented
- CorrectiveActionVerifiedEffective

### Semantic/versioning
- DomainPackActivated
- DomainPackUpgraded
- ExperimentalProfileActivated
- PolicyVersionPinned
- ExtensionAttached
- ExtensionMigrationRecorded

### Output/audit
- ConclusionRecorded
- ConclusionReassessed
- ReportIssued
- InvestigationRecoveryRecorded
- RedactionRecorded

The exact serialization EventType is a SemanticId, allowing future event types without growing a closed core wire enumeration.

## 28. Event correction rule

Immutable events are not edited.

If an event's content was wrong:

- append a corrective/superseding event
- identify the affected event/entity
- explain correction provenance
- rebuild current projection

If persisted bytes are malformed/corrupt, use recovery procedures rather than pretending it was an ordinary semantic correction.

## 29. Derived state

Initially derive rather than persist:

- Leading hypothesis
- claim epistemic posture where feasible
- evidence counts
- blocker counts
- recommended next action
- conclusion dashboard summary
- portfolio/search state
- UI expanded/collapsed state

This protects the model from research changes.

## 30. No generic setters

The following commands are prohibited patterns:

- SetInvestigationState
- SetHypothesisState
- SetClaimConfidence
- SetRootCause
- SetEvidenceScore
- SetEverythingFromAI
- ReplaceInvestigationJson

Specific semantic commands preserve reason, prerequisites, and auditability.

## 31. First implementation minimum command set

For the earliest durable slice, implement first:

1. StartInvestigation
2. ActivateInvestigation
3. RecordObservation
4. RecordHypothesis
5. MarkHypothesisPlausible
6. MarkHypothesisSupported
7. FalsifyHypothesis
8. ReopenHypothesis
9. AdmitEvidence
10. RelateEvidence
11. IdentifyUnknown
12. RecordAssumption
13. RecordContradiction
14. RecordPrediction
15. RecordInvestigationAction
16. RecordFinding
17. EstablishNegativeKnowledge
18. RecordCausalAssertion
19. ConfirmCause
20. RecordCorrectiveAction
21. RecordVerificationCriterion
22. MarkCorrectiveActionImplemented
23. RecordVerification
24. MarkCorrectiveActionVerified
25. RecordConclusion
26. ResolveInvestigation
27. ReopenInvestigation
28. RecordProposal
29. AcceptProposal
30. RejectProposal
31. AttachExtension
32. PinPolicyVersion

Everything else can be added through later slices without invalidating these semantics.
