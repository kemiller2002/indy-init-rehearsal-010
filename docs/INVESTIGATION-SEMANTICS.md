# Investigation Semantics

Status: normative domain semantics for the Root-Cause Investigator Platform.

This document defines what investigation concepts mean, how they differ, which transitions are legal, and which invariants must hold. Product requirements define what the product must do. This document defines the meaning of the state the product is allowed to represent.

The generic kernel must preserve these semantics regardless of UI, storage provider, AI provider, or domain pack.

## 1. Core principle

The system does not store "an answer." It stores an evolving, auditable epistemic state.

At any point, the system should be able to answer:

1. What was observed?
2. What is asserted?
3. What is inferred?
4. What explanations are currently possible?
5. What evidence supports or contradicts each explanation?
6. What remains unknown?
7. What has been ruled out?
8. What assumptions are being relied upon?
9. What action would most usefully change the state of knowledge?
10. What is blocking a stronger conclusion?
11. Why is the current conclusion justified at its current strength?
12. What would cause the conclusion to be reconsidered?

A fluent narrative is not a substitute for those answers.

## 2. Semantic categories

The kernel must distinguish the following categories.

### 2.1 Observation

An Observation is a record of something perceived, measured, reported, or captured without asserting its explanation.

Examples:

- "Requests began returning HTTP 500 at 14:03."
- "The customer reports that the invoice total is wrong."
- "Experiment 42 produced 17% fewer matches."
- "CPU utilization was 96% on node A."

An Observation may be incorrect, imprecise, or second-hand. "Observation" means that the content is being represented as observed or reported, not that it is true.

An Observation must preserve:

- original statement or source payload
- origin
- time observed when known
- time recorded
- scope or subject
- provenance
- verification status

### 2.2 Claim

A Claim is a proposition that can be evaluated as supported, contradicted, unresolved, or out of scope.

Examples:

- "The input dataset was unchanged."
- "Deployment 9 introduced the failure."
- "The denominator calculation differs between versions."

A Claim is not automatically a hypothesis. Claims are building blocks used by hypotheses, conclusions, assumptions, predictions, and evidence interpretation.

### 2.3 Interpretation

An Interpretation is a reasoned reading of one or more observations or evidence items.

Example:

- Observation: latency increased from 40 ms to 900 ms.
- Interpretation: "The increase is consistent with a blocking network call."

Interpretation is not Evidence. It must remain traceable to the Evidence from which it was derived.

### 2.4 Hypothesis

A Hypothesis is a testable explanatory proposition for some material part of the observed problem.

A valid Hypothesis must identify:

- what it claims explains
- the proposed mechanism when known
- its current lifecycle state
- dependencies or assumptions
- supporting evidence
- contradictory evidence
- predictions that follow if it is true
- material unknowns
- provenance

A vague topic such as "network" is not, by itself, a sufficient hypothesis. A better hypothesis is "A retry-loop introduced in version 3.2 is exhausting the outbound connection pool, causing the observed timeout spike."

### 2.5 Assumption

An Assumption is a proposition temporarily accepted for reasoning without sufficient current evidence to treat it as established.

Assumptions must never be silently promoted into facts.

An Assumption may be:

- ExplicitUnverified
- ProvisionallyAccepted
- Verified
- Rejected
- Expired

A material assumption may create an Obligation to verify it.

### 2.6 Evidence

Evidence is information admitted into the investigation for the purpose of evaluating one or more Claims or Hypotheses.

Evidence has two dimensions that must not be conflated:

1. **content**: what the evidence says or contains
2. **epistemic status**: why and how much it should affect the investigation

Evidence should record:

- type
- source
- provenance
- capture time
- applicable scope/version/environment
- verification status
- transformations
- dependency on other evidence
- links to Claims/Hypotheses
- relationship semantics

### 2.7 Evidence relationship

An EvidenceRelationship expresses how a particular evidence item bears on a particular target.

Core relationship kinds:

- Supports
- Contradicts
- Discriminates
- Qualifies
- Corroborates
- FailsToSupport
- Irrelevant
- Supersedes
- DependsOn
- DerivedFrom

The same evidence item may have different relationships to different hypotheses.

"FailsToSupport" is not equivalent to "Contradicts."

### 2.8 Prediction

A Prediction is an expected observable outcome derived from a Hypothesis before the relevant finding is known.

A Prediction should include:

- originating hypothesis
- expected condition
- observable outcome
- scope
- discriminating alternatives when available
- creation time

Predictions created after the result is known must be marked retrospective and cannot carry the same evidentiary weight as prospective predictions.

### 2.9 Investigation Action

An InvestigationAction is deliberate work intended to change the epistemic state.

Subtypes may include:

- Question
- Inspection
- Query
- Measurement
- Comparison
- Experiment
- Simulation
- Reproduction
- ExternalVerification
- CodeReview
- Interview

An InvestigationAction should state:

- purpose
- target unknown/claim/hypotheses
- expected discriminating value
- preconditions
- cost/time/risk when relevant
- required permissions
- predictions
- execution status

### 2.10 Finding

A Finding is the recorded outcome of an InvestigationAction.

A Finding is not automatically a conclusion. It becomes Evidence when admitted and linked to the claims/hypotheses it informs.

### 2.11 Unknown

An Unknown is a material piece of information whose value or truth state is not currently known.

Unknown is not:

- false
- absent
- unsupported
- unimportant

Unknowns may be:

- Blocking
- MaterialNonBlocking
- Informational
- AcceptedResidual
- Resolved
- Superseded

### 2.12 Negative Knowledge

NegativeKnowledge records that an explanation, claim, mechanism, path, or condition has been ruled out, found false, found unsupported, or rendered inapplicable within an explicit scope.

Negative knowledge must include why it was established and the scope in which it applies.

Example:

"Network latency was not causal in build 417 under production traffic profile X, because bypassing the remote call did not change the failure."

This does not mean "network can never be causal."

### 2.13 Contradiction

A Contradiction is an explicit unresolved incompatibility between material pieces of investigation state.

Examples:

- two verified measurements disagree
- one evidence item supports a claim while another materially contradicts it
- a hypothesis predicts X but a trusted finding is not-X

A Contradiction is first-class. It is not merely a UI warning.

### 2.14 Obligation

An Obligation is work required before a transition, conclusion, or policy condition may be satisfied.

Examples:

- verify an assumption
- resolve a material contradiction
- obtain missing evidence
- execute a required experiment
- verify a corrective action
- obtain reviewer approval

An Obligation must identify:

- reason
- source rule/policy
- blocking target
- status
- owner when applicable
- resolution evidence

### 2.15 Causal Mechanism

A CausalMechanism is the proposed process by which a cause produces the observed effect.

A mechanism may include intermediate states or contributing conditions.

The kernel must allow "mechanism unknown" when the domain cannot reasonably establish one, but a domain policy may require a mechanism before confirmation.

### 2.16 Cause

A Cause is a condition represented as having a causal relationship to the investigated outcome.

Cause roles:

- RootCause
- ContributingFactor
- Trigger
- EnablingCondition

These roles are semantically distinct.

A RootCause is not simply "the first thing that changed" or "the deepest why." It is a supported causal explanation that satisfies the applicable confirmation policy.

### 2.17 Mitigation

A Mitigation reduces impact without necessarily removing the cause.

### 2.18 Workaround

A Workaround changes how users or systems operate to avoid the manifestation without necessarily reducing the causal condition itself.

### 2.19 Corrective Action

A CorrectiveAction is intended to change or remove a cause or contributing mechanism.

### 2.20 Preventive Action

A PreventiveAction reduces the likelihood of recurrence or of analogous failures beyond the immediate correction.

### 2.21 Verification Criterion

A VerificationCriterion defines an observable condition that must hold for an action to be considered effective.

### 2.22 Verification

Verification is evidence-backed evaluation of whether a criterion was satisfied.

"Change deployed" is not equivalent to "change verified."

### 2.23 Conclusion

A Conclusion is an authoritative investigation statement at a defined strength.

Conclusion strengths:

- PlausibleExplanation
- LeadingExplanation
- SupportedCause
- ConfirmedCause
- AcceptedWithResidualUncertainty

A conclusion must preserve its basis and unresolved uncertainty.

## 3. Authoritative state versus proposals

The system must distinguish:

- AuthoritativeState
- HumanProposal
- AgentProposal
- DerivedProjection

AI output is a proposal until accepted through a legal transition or an explicitly configured auto-accept policy.

A proposal must never masquerade as direct Evidence merely because it is written confidently.

## 4. Provenance model

Every material record should identify an Actor.

Actor categories:

- Human
- Agent
- System
- ExternalSystem
- ImportedHistorical

Provenance should answer:

- who or what created this?
- when?
- from what source?
- by which transformation?
- under which schema/domain-policy version?
- was it later modified, superseded, verified, or rejected?

Agent provenance should additionally record provider/model/runtime identity when material.

## 5. Evidence quality semantics

The kernel should not reduce evidence quality to one decorative scalar.

Evidence quality may be represented through orthogonal dimensions:

- Directness
- Reliability
- Independence
- Precision
- Completeness
- Freshness
- Reproducibility
- VerificationStatus

Each dimension may use ordinal states defined by policy, such as Unknown, Low, Moderate, High, or domain-specific alternatives.

The system must preserve the underlying dimensions if it renders a simplified summary.

## 6. Evidence independence

Two evidence items are not independent merely because they are separate records.

Examples of dependency:

- two reports quote the same original source
- a chart and a table derive from the same query
- two agents interpret the same log
- multiple tests use the same flawed fixture

The model must allow evidence dependency graphs so the system does not treat repeated copies as corroboration.

## 7. Hypothesis lifecycle

Canonical persisted lifecycle:

```text
Proposed
  -> Plausible
  -> Supported

Proposed/Plausible/Supported
  -> Weakened
  -> Falsified

Any active state
  -> Superseded

Falsified
  -> Reopened
  -> Plausible
```

"Leading" is a comparative posture derived from current evidence, alternatives, and policy. It is not initially a persisted hypothesis lifecycle state.

"Confirmed cause" is represented by a CausalAssertion and its confirmation event. It is not initially a persisted Hypothesis state because a confirmed causal explanation may involve a causal set, multiple hypotheses, conditions, triggers, or contributing factors.

Transitions are not automatic labels. They require evidence and policy.

### 7.1 Proposed -> Plausible

Requires:

- hypothesis is sufficiently specific to evaluate
- no known evidence immediately falsifies it
- mechanism or rationale is stated, or explicitly unknown
- it is relevant to the observation

### 7.2 Plausible -> Supported

Requires:

- at least one material supporting relationship
- support is not solely an unverified assumption
- known material contradictory evidence is visible

### 7.3 Derived leading posture

A hypothesis may be projected as Leading when:

- it currently explains more of the material evidence than alternatives under the applicable comparison policy
- material alternatives remain visible
- the comparison derivation/version is known

Leading does not imply causal confirmation and does not permit suppression of alternatives.

### 7.4 Any active -> Weakened

Occurs when evidence reduces plausibility without making the hypothesis incompatible with the known state.

### 7.5 Any active -> Falsified

Requires evidence or a failed necessary prediction that is materially incompatible with the hypothesis in the stated scope.

Falsification must record:

- falsifying basis
- scope
- policy/rule used
- whether the entire hypothesis or only a variant was falsified

### 7.6 Falsified -> Reopened

Requires:

- new evidence, corrected evidence, corrected scope, or invalidated falsification premise
- explicit reason for reopening

Reopening returns the hypothesis to an active evaluable state, initially Plausible unless policy/evidence supports another explicit transition.

### 7.7 Hypothesis participation in causal confirmation

A Supported hypothesis may be referenced by a proposed CausalAssertion.

ConfirmCause applies to the CausalAssertion and the active cause-confirmation gate. The source hypothesis remains historically Supported unless later evidence weakens, falsifies, or supersedes it.

This separation prevents a one-hypothesis/one-cause assumption and supports causal sets.

## 8. Claim epistemic state

Claims should have an epistemic state separate from their content.

Core states:

- Unassessed
- Unsupported
- PartiallySupported
- Supported
- Contradicted
- Mixed
- EstablishedWithinScope
- FalsifiedWithinScope
- Unknown

A claim's state should be derived from explicit relationships and policy rather than manually overwritten without evidence.

## 9. Unknown lifecycle

```text
Identified
  -> Investigating
  -> Resolved

Identified/Investigating
  -> AcceptedResidual

Identified/Investigating
  -> Superseded
```

A blocking unknown cannot be silently reclassified as non-blocking merely to permit completion. The reclassification requires an explicit policy basis or human acceptance under an allowed policy.

## 10. Assumption lifecycle

```text
ExplicitUnverified
  -> ProvisionallyAccepted
  -> Verified

ExplicitUnverified/ProvisionallyAccepted
  -> Rejected

Any active state
  -> Expired
```

A rejected assumption must trigger reevaluation of conclusions or hypotheses that depend materially on it.

## 11. Investigation Action lifecycle

```text
Proposed
  -> Approved
  -> InProgress
  -> Completed
  -> Interpreted

Proposed/Approved
  -> Rejected

Approved/InProgress
  -> Cancelled

Any active state
  -> Blocked
```

Completion means the action ran or the question was answered. It does not mean the result has been interpreted or admitted as evidence.

## 12. Prediction semantics

Predictions can be:

- Necessary: if H is true, X must occur within the stated scope
- Expected: if H is true, X is expected but absence does not logically falsify H
- Discriminating: H1 predicts X while H2 predicts materially different Y
- NonDiscriminating: multiple active hypotheses predict the same result

The system should prefer discriminating predictions when proposing next actions.

A failed Necessary prediction is potentially falsifying evidence.

## 13. Contradiction lifecycle

```text
Detected
  -> Triaged
  -> Investigating
  -> Resolved

Detected/Triaged/Investigating
  -> AcceptedResidual

Any state
  -> Superseded
```

Resolution must state how the contradiction was resolved:

- one source invalidated
- scope difference discovered
- measurement error established
- temporal/version difference established
- semantic mismatch established
- reconciliation evidence found

Merely choosing one side is not resolution.

## 14. Causality semantics

The system must separate the following:

- temporal association
- correlation
- mechanism consistency
- intervention evidence
- counterfactual evidence
- reproducibility
- elimination of alternatives

A domain policy may require different combinations, but the generic kernel must represent them separately.

### 14.1 Causal support dimensions

A proposed cause may carry explicit support such as:

- TemporalitySatisfied
- MechanismSupported
- InterventionChangedOutcome
- CounterfactualSupported
- Reproduced
- AlternativeExplanationsAddressed
- DoseOrGradientObserved
- DomainConsistencySupported

Not all dimensions apply in all domains.

### 14.2 Counterfactual

A counterfactual asks what would be expected if the proposed cause were absent or materially different.

Examples:

- if the deployment caused the failure, rolling back should remove or materially reduce it
- if the changed parser is causal, running the old parser against the same input should not reproduce the defect

Counterfactual evidence is powerful but must still respect confounding and scope.

### 14.3 Intervention

An intervention intentionally changes a suspected causal factor.

Intervention evidence should record:

- what changed
- what remained controlled
- expected outcome
- actual outcome
- unintended effects
- reversibility

### 14.4 Confounders

A Confounder is a factor that may explain an observed association between a proposed cause and outcome.

Material confounders should be represented as hypotheses, unknowns, or claims rather than buried in narrative notes.

## 15. Cause Confirmation Gate

The generic platform must not allow a ConfirmedCause transition unless all policy-required conditions are satisfied.

The default confirmation gate should require:

1. a specific causal claim exists;
2. material observations are explained or explicitly excluded;
3. material supporting evidence exists;
4. material contradictory evidence is resolved or explicitly accepted under policy;
5. blocking unknowns are resolved or explicitly accepted under policy;
6. required assumptions are verified or explicitly accepted under policy;
7. materially plausible alternative hypotheses are addressed;
8. the causal mechanism is supported or the applicable policy explicitly permits mechanism-unknown confirmation;
9. required prospective/discriminating checks are complete;
10. provenance is sufficient for the conclusion to be audited;
11. no active Aegis invariant violation blocks the transition.

A domain pack may strengthen the gate. It may not silently weaken core provenance and state-integrity guarantees.

## 16. Conclusion posture

The UI and reports should expose a DecisionPosture/ConclusionPosture rather than forcing certainty.

Suggested posture dimensions:

- conclusion strength
- material evidence completeness
- unresolved contradictions
- unresolved blocking unknowns
- assumption burden
- alternative-explanation burden
- verification status

A single numeric confidence score is optional and must never replace these dimensions.

## 17. Root cause versus contributing factors

A RootCause should represent a causal condition whose correction materially addresses the investigated failure under the applicable policy.

A ContributingFactor increases likelihood, severity, or persistence but is not sufficient to carry the full causal explanation.

A Trigger initiates manifestation of an already possible failure mode.

An EnablingCondition makes the failure possible without necessarily initiating it.

An investigation may have:

- one root cause
- multiple root causes
- no confirmed root cause
- only contributing factors
- a systemic cause plus local trigger

The model must not force one "root cause" when the evidence supports a causal set.

## 18. Causal sets

Some failures require multiple jointly necessary conditions.

The kernel must support a CausalSet where:

- no single member is sufficient
- members may have different roles
- removing one member may prevent recurrence
- the set as a whole explains the outcome

This prevents the "pick one deepest cause" failure common in simplistic RCA.

## 19. Negative knowledge semantics

NegativeKnowledge must carry scope.

Valid examples:

- "H1 falsified for release 3.2 under load profile A."
- "Dataset drift ruled out for experiment E42 based on checksum comparison."

Invalid overgeneralization:

- "Dataset drift can never be causal."

When new evidence conflicts with negative knowledge, the system should create a reevaluation obligation rather than silently discarding the prior record.

## 20. Corrective action lifecycle

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

"Implemented" must never project as "Resolved" when verification is required.

## 21. Investigation lifecycle

Suggested canonical persisted lifecycle:

```text
Draft
  -> Active
  -> AwaitingEvidence
  -> Active
  -> AwaitingVerification
  -> Resolved
  -> Closed

Resolved/Closed
  -> Active via InvestigationReopened

Any non-destructive state
  -> Archived
```

Lifecycle semantics:

- Draft: initial capture incomplete
- Active: investigation is actively progressing
- AwaitingEvidence: progress depends primarily on external evidence/action
- AwaitingVerification: causal conclusion/correction exists but outcome verification is pending
- Resolved: resolution criteria satisfied
- Closed: administrative closure after resolution
- Archived: removed from active workflow without erasing history

Reopened is an authoritative event and historical fact, not initially a durable lifecycle case. The event transitions the current lifecycle back to Active while preserving the prior resolution/closure.

## 22. Resolution Gate

An Investigation may enter Resolved only when the configured policy is satisfied.

Default requirements:

- conclusion posture is recorded
- no unresolved blocking investigation obligations
- required corrective action status is satisfied
- required verification criteria are satisfied
- residual uncertainty is explicit
- final state is internally consistent
- persistence of authoritative state is confirmed

A policy may allow "resolved with accepted residual uncertainty," but acceptance must be explicit and attributable.

## 23. Event model

Material state changes should produce semantic events.

Examples:

- InvestigationCreated
- ObservationRecorded
- ClaimCreated
- HypothesisProposed
- HypothesisStateChanged
- EvidenceAdmitted
- EvidenceRelationshipAdded
- UnknownIdentified
- AssumptionAccepted
- ContradictionDetected
- ActionProposed
- FindingRecorded
- NegativeKnowledgeEstablished
- CauseProposed
- CauseConfirmed
- CorrectiveActionImplemented
- VerificationCompleted
- InvestigationResolved
- InvestigationReopened

Each event should include:

- event id
- investigation id
- sequence/version
- event kind
- actor
- timestamp
- prior version
- payload
- reason when applicable
- evidence references when required
- schema/policy/domain-pack version

Events are historical facts about state change. They must not be rewritten to make the final story cleaner.

## 24. Invariants

The generic kernel must enforce at least these invariants.

### I-01 Original source preservation
A structured projection may not destroy the original source statement.

### I-02 Evidence/inference separation
Interpretation or AI reasoning may not be stored as direct evidence without explicit source semantics.

### I-03 No silent hypothesis deletion
A hypothesis that influenced the investigation may not disappear from history.

### I-04 Falsification provenance
A falsified hypothesis must identify the falsifying basis and scope.

### I-05 Reopen requires reason
A falsified, resolved, or closed state may not reopen without provenance.

### I-06 Unknown is not false
Unknown must never be projected as false merely because no evidence exists.

### I-07 Failure to support is not contradiction
Absence of supporting evidence must not automatically become contradictory evidence.

### I-08 Correlated evidence is not independent corroboration
Known evidence dependencies must be preserved.

### I-09 Contradictions cannot be hidden by conclusion
A conclusion may not erase unresolved contradictory evidence.

### I-10 Blocking obligations block
A transition protected by an unresolved blocking obligation must be illegal.

### I-11 Proposal is not authoritative state
Agent proposals may not mutate authoritative state outside configured legal acceptance policy.

### I-12 Prediction timing preserved
Retrospective predictions must not be represented as prospective.

### I-13 Implemented is not verified
Corrective action implementation may not imply verification.

### I-14 Mitigation is not root-cause correction
Mitigation may not be relabeled as causal correction without evidence.

### I-15 Scope preserved
Evidence, negative knowledge, claims, and conclusions must preserve material scope/version/environment.

### I-16 No decorative confidence
A confidence representation must expose its evidentiary basis and unresolved uncertainty.

### I-17 Historical policy is explainable
A historical transition must remain explainable under the policy/version in force when it occurred.

### I-18 Persistence truth
Pending, failed, or conflicted persistence may not be shown as successfully committed authoritative remote state.

### I-19 Cause confirmation gate
ConfirmedCause cannot exist unless the active policy's gate is satisfied.

### I-20 Resolution gate
Resolved cannot exist unless the active policy's resolution gate is satisfied.

## 25. Capability model

The state should expose legal capabilities, not force the UI to infer them.

Examples:

- ProposeHypothesis
- AdmitEvidence
- LinkEvidence
- RecordContradiction
- ProposeAction
- ExecuteAction
- RecordFinding
- FalsifyHypothesis
- ReopenHypothesis
- ProposeCause
- ConfirmCause
- AcceptResidualUncertainty
- ProposeCorrectiveAction
- MarkImplemented
- VerifyAction
- ResolveInvestigation
- ReopenInvestigation
- GenerateReport

Each capability should provide:

- availability
- blocking reasons
- required evidence/obligations
- policy source

## 26. Typed model direction

The exact implementation may evolve, but the F# domain should trend toward semantic types similar to:

```fsharp
type EpistemicSource =
    | HumanObservation
    | ExternalRecord
    | InstrumentMeasurement
    | ImportedEvidence
    | AgentInference
    | DerivedCalculation

type HypothesisState =
    | Proposed
    | Plausible
    | Supported
    | Weakened
    | Falsified
    | Superseded

type HypothesisComparisonPosture =
    | NotCompared
    | CandidateLeader
    | Leading
    | Tied

type UnknownState =
    | Identified
    | Investigating
    | AcceptedResidual
    | Resolved
    | Superseded

type CauseRole =
    | RootCause
    | ContributingFactor
    | Trigger
    | EnablingCondition

type CoreEvidenceRelationshipKind =
    | Supports
    | Contradicts
    | Discriminates
    | Qualifies
    | Corroborates
    | FailsToSupport
    | Irrelevant
    | Supersedes
    | DependsOn
    | DerivedFrom

type SemanticId =
    {
        Namespace : string
        Name : string
        Version : string
    }

type EvidenceRelationshipKind =
    | CoreRelationship of CoreEvidenceRelationshipKind
    | ExtensionRelationship of SemanticId

type ConclusionStrength =
    | PlausibleExplanation
    | LeadingExplanation
    | SupportedCause
    | ConfirmedCause
    | AcceptedWithResidualUncertainty
```

Primitive strings should not be used where doing so permits semantically invalid states.

## 27. UI semantic obligations

The UI must project domain meaning accurately.

At minimum:

- observations must be visually distinguishable from inference
- agent proposals must be visually distinguishable from accepted state
- falsified hypotheses remain inspectable
- blocking unknowns and obligations are visible
- material contradictions are visible
- unavailable capabilities explain why they are unavailable
- pending/conflicted persistence is visible
- implemented corrective action is not shown as verified
- conclusion strength and residual uncertainty are visible together
- evidence dependency/correlation can be inspected

## 28. Agent reasoning contract

An agent assisting an investigation should:

1. preserve user observations verbatim before normalizing;
2. separate observation from interpretation;
3. propose multiple plausible hypotheses when warranted;
4. state assumptions;
5. identify unknowns;
6. seek discriminating evidence;
7. challenge the leading explanation;
8. avoid treating repetition as independent corroboration;
9. identify evidence dependencies;
10. avoid causal claims based solely on temporal sequence or correlation;
11. propose counterfactual or intervention checks when appropriate;
12. explain why a requested transition is blocked;
13. preserve falsified paths;
14. distinguish mitigation from correction;
15. request verification after implementation;
16. surface residual uncertainty in conclusions.

An agent must not:

- fabricate evidence
- silently change source statements
- mark its own inference as direct observation
- silently resolve contradictions
- silently accept assumptions
- delete inconvenient hypotheses
- invent certainty to satisfy completion pressure
- treat its own confidence as evidentiary proof

## 29. Domain-pack extension rules

A domain pack may:

- add specialized evidence types
- add hypothesis/cause taxonomies
- add policy-required checks
- add domain-specific causal-support dimensions
- add report sections
- add action templates
- strengthen confirmation/resolution gates
- add UI projections

A domain pack must not:

- redefine Observation to include inference
- turn agent output into direct evidence
- erase provenance requirements
- make unknown equivalent to false
- bypass authoritative-state validation
- silently weaken historical integrity

## 30. EDF specialization mapping

The EDF domain pack should map naturally onto the generic semantics.

Examples:

- experiment result -> Observation/Finding/Evidence
- regression hypothesis -> Hypothesis
- model assumption -> Assumption
- unknown effect -> Unknown
- disproven mechanism -> NegativeKnowledge
- metric anomaly -> Observation
- competing explanation -> Hypothesis
- next experiment -> InvestigationAction
- experimental prediction -> Prediction
- evidence dependency/cycle -> Evidence dependency graph
- root explanation -> Cause/Conclusion
- follow-up experiment -> Verification or InvestigationAction depending on intent

EDF-specific semantics should extend these primitives rather than replace them.

## 31. Design test

The model is doing its job if two investigators can disagree about the likely cause while still agreeing on:

- what was observed
- what evidence exists
- which assumptions are being made
- which hypotheses remain viable
- what is contradicted
- what is unknown
- which actions are legal
- what evidence would change the conclusion

The system should make disagreement explicit and investigable rather than forcing premature consensus.


## 32. Semantic evolution boundary

The canonical semantics are not a declaration that the research ontology is finished.

This document stabilizes the minimum generic meanings needed to preserve epistemic integrity. Domain methodology may continue to evolve through the extension architecture defined in [SEMANTIC-EVOLUTION.md](SEMANTIC-EVOLUTION.md).

### 32.1 CoreStable meanings

Core meanings should change rarely and only with explicit compatibility analysis.

Examples include:

- Observation does not mean inference.
- Unknown does not mean false.
- Evidence retains provenance.
- Proposals are distinct from authoritative state.
- Historical transitions retain their original meaning.
- Blocking obligations block the transitions they protect.
- Verification is distinct from implementation.

### 32.2 Extensible classifications

Taxonomies expected to grow should allow domain-specific qualified extensions.

Examples include:

- evidence relationship subtypes
- evidence categories
- causal-support dimensions
- action kinds
- metric definitions
- domain-specific cause classifications
- domain-specific conclusion or review metadata

A core case may coexist with extension cases. Extension cases do not become generic truth merely because one domain uses them.

### 32.3 Research concepts

A research construct may be represented as an extension without implying that the platform endorses its validity.

The platform can therefore investigate whether a construct is useful while preserving records created with it.

### 32.4 Semantic version binding

A material domain-specific assertion should be interpretable against the semantic version in force when it was created.

A label alone is insufficient when the meaning has changed.

### 32.5 No retroactive ontology

New semantic constructs must not be projected backward into historical data as though the original investigator had recorded them.

A newer projection may derive a new interpretation, but the derivation and its time/version must be explicit.
