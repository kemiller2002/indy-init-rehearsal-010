# Domain Test Matrix

Status: pre-kickoff planning specification, not executable competition application tests.

This matrix translates the command/event/state contracts into test obligations. If organizer rules permit pre-written executable acceptance tests, these scenarios may be promoted into code. Otherwise, implement them after kickoff from this specification.

## 1. Test pattern

Every authoritative command test should use:

```text
Given authoritative state S
And semantic manifest M
And policy set P
And actor context A
When command C is decided
Then decision is D
And blocker/review output is B
And emitted event kinds are E

When E is evolved
Then resulting state is S2
And capabilities are K
```

For event evolution:

```text
Given state S
When event E is evolved
Then projection is deterministic
And no network, AI, clock, random, or persistence dependency is required
```

## 2. Cross-cutting command tests

Every command must have, where applicable:

- happy path
- wrong lifecycle/state
- stale aggregate version
- unsupported required semantics
- unauthorized actor
- policy blocker
- review required
- idempotent retry
- event attribution
- evolve result
- capability change
- serialization round trip
- unknown optional extension preservation

## 3. Investigation tests

### StartInvestigation

Prove:

- can start when aggregate does not exist
- cannot start over an existing InvestigationId
- original problem statement is preserved
- semantic manifest is recorded
- Draft projection results
- ExpectedVersion may be absent only for creation
- provider metadata is absent from domain identity

### ActivateInvestigation

Prove:

- Draft -> Active when activation requirements hold
- blocked when required initial state is missing
- cannot activate Closed/Resolved by using generic activation instead of ReopenInvestigation
- policy blockers are explainable

### ResolveInvestigation

Prove:

- blocked by unresolved blocking obligations
- blocked by required unverified corrective action
- blocked by unresolved required verification
- may allow explicit accepted residual uncertainty only when policy permits
- emits InvestigationResolved
- does not delete active history
- capability projection changes appropriately

### ReopenInvestigation

Prove:

- Resolved/Closed -> Active with explicit basis
- prior resolution/closure events remain
- cannot reopen without allowed basis
- new material contradiction can enable reopening

## 4. Observation tests

### RecordObservation

Prove:

- original text required
- original text preserved byte/semantic-equivalently according to serialization contract
- actor/provenance recorded
- normalization absent unless separately added

### AddObservationNormalization

Prove:

- source Observation required
- normalization does not overwrite original
- multiple normalizations/supersession remain historical

## 5. Hypothesis tests

### RecordHypothesis

Prove:

- creates Proposed
- vague/invalid shape may be blocked by policy/validation
- no Leading/Confirmed lifecycle state produced

### MarkHypothesisPlausible

Prove:

- Proposed -> Plausible
- Falsified cannot jump directly to Plausible except through ReopenHypothesis
- immediate known falsifier can block

### MarkHypothesisSupported

Prove:

- Plausible/Weakened -> Supported only with required support
- missing evidence creates blocker
- unsupported assumption alone cannot satisfy policy when prohibited
- no causal confirmation occurs

### FalsifyHypothesis

Prove:

- requires falsifying basis and scope
- emits HypothesisFalsified
- falsified hypothesis remains visible/history-preserved
- optional NegativeKnowledge may be established through explicit additional event
- FailsToSupport alone does not necessarily satisfy falsification

### ReopenHypothesis

Prove:

- only from Falsified under first profile
- requires new/corrected evidence, scope, or invalidated premise
- returns Plausible
- prior falsification remains historical

### Derived Leading

Prove:

- can change without lifecycle event
- uses comparison derivation/policy version
- never grants ConfirmCause authority by itself

## 6. Evidence tests

### AdmitEvidence

Prove:

- provenance/source required according to policy
- AI interpretation is not accepted as direct evidence merely by type confusion
- extension categories round-trip
- artifact metadata can be external

### VerifyEvidence

Prove:

- verification basis recorded
- does not overwrite original content
- invalidation can trigger downstream reevaluation obligations

### SupersedeEvidence

Prove:

- old evidence remains historical
- effective-current projection points to superseding record/state
- dependent relationships/conclusions can be flagged for reassessment

### Evidence dependency

Prove:

- two records sharing source can be linked
- dependency prevents independent-corroboration projection where policy requires
- dependency itself has provenance

## 7. Evidence relationship tests

### RelateEvidence

For each core relationship currently retained, prove valid creation:

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

Also prove:

- extension relationship SemanticId can be preserved
- FailsToSupport != Contradicts
- scope mismatch can prevent invalid relationship
- target must exist/resolve
- no universal evidence score is produced

OQ-012 remains open, so tests should make it easy to remove a core relationship without rewriting unrelated semantics.

## 8. Assumption tests

Prove:

- RecordAssumption -> ExplicitUnverified
- provisional acceptance has provenance
- verification requires basis
- rejection triggers dependent reevaluation where material
- expiry does not rewrite earlier verification/acceptance
- assumption never silently becomes fact

## 9. Unknown tests

Prove:

- IdentifyUnknown -> Identified
- Blocking unknown blocks protected transition
- ResolveUnknown requires resolution basis
- AcceptUnknownAsResidual requires policy authority
- materiality changes are explicit
- Unknown never projects as False

## 10. Contradiction tests

Prove:

- contradiction participants are preserved
- material contradiction blocks confirmation when policy says so
- ResolveContradiction requires resolution basis
- accepting residual contradiction is policy-controlled
- resolving contradiction does not erase the conflicting evidence

## 11. Prediction tests

Prove:

- prospective prediction recorded before result
- later result does not mutate original prediction text
- retrospective prediction is marked
- necessary prediction failure can support falsification under policy
- discriminating prediction can reference multiple hypotheses

## 12. Investigation action tests

Prove all structural transitions:

- Proposed -> Approved
- Proposed -> Rejected
- Approved -> InProgress
- Approved/InProgress -> Blocked
- Blocked -> prior runnable posture
- InProgress -> Completed
- Completed -> Interpreted
- Approved/InProgress -> Cancelled

Also prove:

- Completed does not imply Evidentiary support
- result capture is Finding, not Conclusion

## 13. Finding tests

Prove:

- Finding references source action
- original result preserved
- normalization separate
- AdmitFindingAsEvidence produces/link distinct Evidence
- finding can remain uninterpreted

## 14. Negative knowledge tests

Prove:

- established record has explicit scope and evidence basis
- challenge preserves original record
- supersession is historical
- AI context can retrieve applicable scoped negative knowledge
- negative knowledge from version/environment A is not universalized to B

## 15. Obligation tests

Prove:

- derived blocker does not create duplicate durable obligations repeatedly
- AcceptObligation gives durable identity
- policy source retained
- WaiveObligation works only when rule permits
- core invariant obligation cannot be waived
- resolving obligation updates protected capabilities

## 16. Causal assertion tests

### RecordCausalAssertion

Prove:

- starts Proposed
- supports one or multiple causal members
- member roles can include root/contributing/trigger/enabling
- extension roles survive round trip
- no DAG requirement exists

### ConfirmCause

Prove blockers for:

- no material supporting evidence
- unresolved required contradiction
- blocking unknown
- unverified required assumption
- unaddressed material alternative
- missing required mechanism
- missing required discriminating check
- missing review
- unsupported required extension

Prove success:

- emits CauseConfirmed for CausalAssertion
- does not emit HypothesisConfirmed
- supports causal set
- policy/version attribution preserved

### ReopenConfirmedCause

Prove:

- prior CauseConfirmed remains historical
- current causal assertion becomes Reopened
- new evidence/contradiction basis required
- dependent conclusion/corrective state can generate reevaluation obligations

## 17. Corrective action tests

Prove:

- Proposed -> Approved -> InProgress -> Implemented
- Implemented does not equal VerifiedEffective
- verification criterion required when policy says so
- AwaitingVerification -> VerifiedEffective on satisfied verification
- AwaitingVerification -> VerificationFailed on failed result
- failed verification can reopen investigation/cause work
- ReworkRequired loops back through explicit action

## 18. Verification tests

Prove:

- criterion definition remains separate from result
- result references evidence/findings
- Satisfied/Failed/Inconclusive preserved
- superseding verification does not delete old result
- report shows actual current verification posture

## 19. Conclusion tests

Prove:

- conclusion is snapshot, not root authority
- cannot assert ConfirmedCause strength absent confirmed causal assertion/policy
- ReassessConclusion creates new historical record
- residual unknowns/contradictions remain listed
- current conclusion projection changes without deleting prior conclusion

## 20. Proposal tests

Prove:

- proposal records source/model provenance
- proposal does not mutate authoritative target state
- accept creates/re-runs target command
- state/version/policy change can make proposal stale
- target blocker prevents ProposalAccepted
- rejection remains proposal/audit history
- extension proposal can be validated by domain pack

## 21. Extension tests

Prove:

- optional unknown extension survives load/save
- required unknown extension prevents unsafe writes
- domain pack activation records version
- policy pin records version
- side-by-side semantic migration preserves old payload
- experimental deactivation does not delete historical extension
- extension cannot bypass core invariant

## 22. Command/event purity tests

### decide

Property:

For identical state, command, policies, actor, and semantics, decision is equivalent.

Must not depend on:

- current wall clock not in command
- random
- network
- AI
- GitHub
- local storage

### evolve

Property:

For identical prior state and event, output projection is identical.

Must not depend on infrastructure.

## 23. Version/concurrency tests

Prove:

- correct ExpectedAggregateVersion can proceed
- stale ExpectedAggregateVersion returns Conflict before mutation
- stale command is revalidated after refresh
- a command legal at N is not presumed legal at N+1
- committed event versions are monotonic
- duplicate idempotency key does not duplicate events

## 24. Event-history tests

Prove:

- event IDs unique
- event order uses AggregateVersion
- correction appends event
- prior events immutable
- historical event retains policy/semantic manifest
- unknown optional extension event preserved
- malformed duplicate EventId fails integrity loading

## 25. Capability tests

For each protected command, capabilities(state, actor, policy) must expose:

- Available when legal
- Unavailable with blockers
- ReviewRequired when review is sole unmet gate
- SemanticsUnavailable when required pack/extension missing

UI-specific state must not alter the result.

## 26. Infrastructure boundary tests

Domain project architecture check should prove no dependency on:

- GitHub SDK/types
- browser DOM
- Limen runtime
- AI provider SDK
- HTTP client
- persistence implementation
- auth-token type

Application/Persistence/AI/Web depend inward.

## 27. EDF anti-lock-in tests

Before EDF pack ships, prove:

- adding EDF metric requires no CoreCommand/CoreEntity change
- ContextCoverage extension can change version side-by-side
- UnknownEffect maps through generic Unknown + extension
- EDF policy can strengthen ConfirmCause without modifying core transition code
- removing rejected experimental construct leaves generic investigation readable
- EDF vN and vN+1 can evaluate same immutable snapshot independently

## 28. Traceability requirement

Every implemented test should reference:

- requirement ID(s)
- acceptance scenario ID(s)
- command/event/state contract section
- source code under test
- evidence result

ROS completion should not mark a protected transition implemented until its required test evidence exists.

## 29. Organizer boundary

This file is a planning/test specification.

Before kickoff, verify event rules regarding pre-written executable tests. If pre-written application tests are not allowed, do not convert this matrix into competition implementation test code until kickoff.

The semantic requirements, scenarios, and test obligations remain the baseline.
