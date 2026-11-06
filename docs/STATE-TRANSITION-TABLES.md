# State Transition Tables

Status: normative first implementation profile.

These tables define initial legal lifecycle transitions. Policy may add prerequisites or block an otherwise structurally legal transition. Domain packs may strengthen rules. No domain pack may create transitions that violate core invariants.

This document deliberately distinguishes lifecycle state from derived posture.

## 1. Investigation lifecycle

Persisted states:

- Draft
- Active
- AwaitingEvidence
- AwaitingVerification
- Resolved
- Closed
- Archived

Reopened is an event that normally returns Resolved/Closed to Active.

| From | Command | To | Minimum structural condition |
| --- | --- | --- | --- |
| none | StartInvestigation | Draft | valid identity/source/manifest |
| Draft | ActivateInvestigation | Active | activation policy satisfied |
| Active | MarkAwaitingEvidence | AwaitingEvidence | open evidence dependency/reason |
| AwaitingEvidence | ActivateInvestigation | Active | wait cleared/work resumed |
| Active | MarkAwaitingVerification | AwaitingVerification | verification work exists |
| AwaitingVerification | ActivateInvestigation | Active | verification failed/new work |
| AwaitingVerification | ResolveInvestigation | Resolved | resolution gate |
| Active | ResolveInvestigation | Resolved | policy permits and resolution gate |
| Resolved | CloseInvestigation | Closed | closure policy |
| Resolved/Closed | ReopenInvestigation | Active | explicit reopening basis |
| any non-archived state | ArchiveInvestigation | Archived | archive policy |
| Archived | ActivateInvestigation | Active | explicit unarchive/reactivation policy |

## 2. Hypothesis lifecycle

Persisted states:

- Proposed
- Plausible
- Supported
- Weakened
- Falsified
- Superseded

Leading is initially derived.

| From | Command | To | Minimum structural condition |
| --- | --- | --- | --- |
| none | RecordHypothesis | Proposed | testable explanatory proposition |
| Proposed | MarkHypothesisPlausible | Plausible | evaluable/relevant/no immediate falsifier |
| Plausible | MarkHypothesisSupported | Supported | material support |
| Plausible/Supported | MarkHypothesisWeakened | Weakened | weakening basis |
| Proposed/Plausible/Supported/Weakened | FalsifyHypothesis | Falsified | scoped falsifying basis |
| Proposed/Plausible/Supported/Weakened | SupersedeHypothesis | Superseded | explicit reason |
| Falsified | ReopenHypothesis | Plausible | new/corrected evidence, scope, or premise |
| Weakened | MarkHypothesisPlausible | Plausible | strengthening basis |
| Weakened | MarkHypothesisSupported | Supported | sufficient support |
 
Cause confirmation is not a Hypothesis lifecycle transition. A supported hypothesis may participate in a separate CausalAssertion, which owns Confirmed/Reopened state.

No generic SetHypothesisState exists.

## 3. Assumption lifecycle

States:

- ExplicitUnverified
- ProvisionallyAccepted
- Verified
- Rejected
- Expired

| From | Command | To |
| --- | --- | --- |
| none | RecordAssumption | ExplicitUnverified |
| ExplicitUnverified | ProvisionallyAcceptAssumption | ProvisionallyAccepted |
| ExplicitUnverified/ProvisionallyAccepted | VerifyAssumption | Verified |
| ExplicitUnverified/ProvisionallyAccepted | RejectAssumption | Rejected |
| ExplicitUnverified/ProvisionallyAccepted/Verified | ExpireAssumption | Expired |

If later evidence invalidates a previously verified assumption, use explicit reassessment/supersession rather than rewriting the old verification.

## 4. Evidence lifecycle

Evidence is append/supersede rather than a broad workflow.

| State | Command | Result |
| --- | --- | --- |
| none | AdmitEvidence | Active |
| Active | VerifyEvidence | Active + verification assessment |
| Active | CorrectEvidenceMetadata | Active + corrective event |
| Active | SupersedeEvidence | Superseded |
| Active/Superseded | MarkEvidenceArtifactUnavailable | availability changes |
| Active/Superseded | RecordEvidenceIntegrityMismatch | integrity status changes |

Original evidence content is never overwritten.

## 5. Investigation action lifecycle

States:

- Proposed
- Approved
- InProgress
- Blocked
- Completed
- Interpreted
- Rejected
- Cancelled

| From | Command | To |
| --- | --- | --- |
| none | RecordInvestigationAction | Proposed |
| Proposed | ApproveInvestigationAction | Approved |
| Proposed | RejectInvestigationAction | Rejected |
| Approved | StartInvestigationAction | InProgress |
| Approved/InProgress | BlockInvestigationAction | Blocked |
| Blocked | UnblockInvestigationAction | Approved or InProgress according to prior state |
| Approved/InProgress | CancelInvestigationAction | Cancelled |
| InProgress | CompleteInvestigationAction | Completed |
| Completed | InterpretInvestigationAction | Interpreted |

## 6. Unknown lifecycle

States:

- Identified
- Investigating
- Resolved
- AcceptedResidual
- Superseded

| From | Command | To |
| --- | --- | --- |
| none | IdentifyUnknown | Identified |
| Identified | StartInvestigatingUnknown | Investigating |
| Identified/Investigating | ResolveUnknown | Resolved |
| Identified/Investigating | AcceptUnknownAsResidual | AcceptedResidual |
| Identified/Investigating | SupersedeUnknown | Superseded |

Materiality is orthogonal and policy-controlled.

## 7. Negative knowledge lifecycle

States:

- Active
- Challenged
- Superseded

| From | Command | To |
| --- | --- | --- |
| none | EstablishNegativeKnowledge | Active |
| Active | ChallengeNegativeKnowledge | Challenged |
| Active/Challenged | SupersedeNegativeKnowledge | Superseded |

Challenge preserves the original scoped record.

## 8. Contradiction lifecycle

States:

- Detected
- Triaged
- Investigating
- Resolved
- AcceptedResidual
- Superseded

| From | Command | To |
| --- | --- | --- |
| none | RecordContradiction | Detected |
| Detected | TriageContradiction | Triaged |
| Detected/Triaged | StartInvestigatingContradiction | Investigating |
| Detected/Triaged/Investigating | ResolveContradiction | Resolved |
| Detected/Triaged/Investigating | AcceptContradictionAsResidual | AcceptedResidual |
| Detected/Triaged/Investigating | SupersedeContradiction | Superseded |

Resolution requires an explicit resolution basis.

## 9. Obligation lifecycle

Durable states:

- Accepted
- InProgress
- Resolved
- Waived
- Superseded

Derived obligations may exist before Accepted.

| From | Command | To |
| --- | --- | --- |
| derived candidate | AcceptObligation | Accepted |
| Accepted | StartObligation | InProgress |
| Accepted/InProgress | ResolveObligation | Resolved |
| Accepted/InProgress | WaiveObligation | Waived if policy permits |
| Accepted/InProgress | SupersedeObligation | Superseded |

Core invariant obligations are never waivable.

## 10. Causal assertion lifecycle

First-profile states/postures:

- Proposed
- Confirmed
- Reopened
- Superseded

"Supported" should initially be derived from evidence/support assessments.

| From | Command | To |
| --- | --- | --- |
| none | RecordCausalAssertion | Proposed |
| Proposed | ConfirmCause | Confirmed |
| Confirmed | ReopenConfirmedCause | Reopened |
| Proposed/Reopened | SupersedeCausalAssertion | Superseded |
| Reopened | ConfirmCause | Confirmed |

This avoids prematurely persisting a causal support taxonomy.

## 11. Corrective action lifecycle

States:

- Proposed
- Approved
- InProgress
- Implemented
- AwaitingVerification
- VerifiedEffective
- VerificationFailed
- ReworkRequired
- Cancelled

| From | Command | To |
| --- | --- | --- |
| none | RecordCorrectiveAction | Proposed |
| Proposed | ApproveCorrectiveAction | Approved |
| Proposed/Approved | CancelCorrectiveAction | Cancelled |
| Approved | StartCorrectiveAction | InProgress |
| InProgress | MarkCorrectiveActionImplemented | Implemented |
| Implemented | StartCorrectiveActionVerification | AwaitingVerification |
| AwaitingVerification | MarkCorrectiveActionVerified | VerifiedEffective |
| AwaitingVerification | MarkCorrectiveActionVerificationFailed | VerificationFailed |
| VerificationFailed | RequireCorrectiveActionRework | ReworkRequired |
| ReworkRequired | StartCorrectiveAction | InProgress |

## 12. Proposal lifecycle

States:

- Active
- Revised
- Accepted
- Rejected
- Deferred
- Stale

| From | Command | To |
| --- | --- | --- |
| none | RecordProposal | Active |
| Active/Revised | EditProposal | Revised |
| Active/Revised/Deferred | AcceptProposal | Accepted only if target command succeeds |
| Active/Revised/Deferred | RejectProposal | Rejected |
| Active/Revised | DeferProposal | Deferred |
| Active/Revised/Deferred | MarkProposalStale | Stale |

A blocked target command does not produce ProposalAccepted.

## 13. Review lifecycle

States:

- Requested
- Approved
- Rejected
- Withdrawn
- Expired

| From | Command | To |
| --- | --- | --- |
| none | RequestReview | Requested |
| Requested | ApproveReview | Approved |
| Requested | RejectReview | Rejected |
| Requested | WithdrawReview | Withdrawn |
| Requested | ExpireReview | Expired |

A material state change may make an Approved review stale for a later transition without rewriting the historical review.

## 14. Conclusion behavior

Conclusions are snapshot records, not mutable lifecycle state.

```text
ConclusionRecorded C1
  -> later ConclusionReassessed C2
  -> C1 remains historical
```

Current conclusion is derived as effective-current projection.

## 15. Domain-pack and policy version behavior

- Domain-pack activation/upgrades are explicit events.
- Optional packs may be deactivated if no required semantics become unavailable.
- Policy pins change through explicit events.
- Historical events remain associated with their original policy/semantic versions.

## 16. Transition evaluation order

For every command:

1. validate command/schema;
2. verify required semantic dependencies;
3. compare expected aggregate version;
4. enforce core invariant firewall;
5. validate structural transition;
6. evaluate core/domain/experimental/organization policy;
7. evaluate actor authorization;
8. evaluate review requirements;
9. propose semantic events;
10. persist atomically;
11. evolve projection and capabilities.

A policy can block a structurally legal transition.

A policy cannot make a structurally impossible core transition legal.

## 17. Implementation rule

Do not duplicate transition tables in UI, AI adapters, API handlers, and persistence.

There must be one domain decision path.

Table-driven tests should cover:

- state x command legality
- policy blockers
- emitted event kinds
- resulting projection
- capability changes
- version conflict behavior
