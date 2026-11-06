# Policy and Transition Engine

Status: normative execution architecture direction.

This document defines how authoritative investigation state changes are decided. It exists to keep legality, blockers, obligations, review requirements, and policy evolution out of UI code, AI prompts, and storage adapters.

## 1. Core principle

A state-changing request is never "valid because the caller asked for it."

Every consequential change follows:

```text
Command
  -> semantic compatibility check
  -> invariant firewall
  -> policy evaluation
  -> capability/permission evaluation
  -> decision
  -> events
  -> new projection
```

The same command evaluated against the same authoritative state, semantic versions, policy versions, and actor capabilities should produce the same semantic decision.

## 2. Responsibilities

The engine owns:

- legal transition evaluation
- invariant enforcement
- blocker generation
- obligation generation
- review requirements
- semantic compatibility checks
- policy version attribution
- decision explanations
- event proposals for allowed transitions

The engine does not own:

- visual rendering
- GitHub paths
- HTTP transport
- AI model prompting
- report layout
- search indexing

## 3. Command envelope

Every authoritative request should use a command envelope.

Conceptual fields:

```text
CommandEnvelope
- CommandId
- InvestigationId
- ExpectedAggregateVersion
- Actor
- RequestedAt
- CommandKind
- Payload
- SemanticManifestSeenByCaller
- CorrelationId optional
- CausationId optional
- IdempotencyKey optional
```

The command must carry the aggregate version against which the caller made the decision when concurrency matters.

## 4. Decision model

The decision result should be explicit.

```fsharp
type Decision =
    | Allowed of ProposedEvent list
    | Blocked of Blocker list
    | RequiresReview of ReviewRequirement list
    | UnsupportedSemantics of SemanticDependency list
    | Conflict of VersionConflict
    | Duplicate of PriorDecisionRef
```

A generic exception must not be the normal representation of a domain decision.

## 5. Blockers

A Blocker explains why a requested transition is not currently legal.

Required fields:

- BlockerId
- code
- human-readable explanation
- source layer
- source rule/policy id and version
- target command/capability
- related entity references
- related obligation references
- whether resolvable by current actor
- suggested resolution capability when available

Example:

```text
code: unresolved-material-contradiction
source: edf:confirmation-policy@0.4
target: ConfirmCause
related: contradiction C-17
resolution: ResolveContradiction
```

## 6. Blocker classes

Suggested generic classes:

- InvariantViolation
- MissingRequiredEvidence
- BlockingUnknown
- UnresolvedContradiction
- UnverifiedAssumption
- MissingVerification
- MissingPermission
- MissingReview
- UnsupportedSemantics
- StaleVersion
- InvalidLifecycle
- PolicyPrerequisite
- PersistenceConflict
- ExtensionUnavailable

These are diagnostic categories, not an exhaustive domain ontology.

## 7. Obligations

Policies may generate durable Obligations.

A blocker and an obligation are related but not identical.

A Blocker answers:
"Why can I not do this now?"

An Obligation answers:
"What work remains required?"

An obligation may exist even when no user is currently attempting the blocked transition.

## 8. Obligation derivation

Obligations may be:

- EventCreated: explicitly created and persisted because the obligation itself has lifecycle/history.
- Derived: deterministically projected from current state and active policy.
- Accepted: a derived obligation that was explicitly acknowledged/assigned and therefore gains durable identity.

The implementation must define which obligations need historical identity.

It must not silently create duplicated obligations each time policy is evaluated.

## 9. Policy layers

Policy evaluation order:

```text
Core invariant firewall
    ↓
Core policy
    ↓
Domain-pack policy
    ↓
Experimental profile policy
    ↓
Organization policy
    ↓
Investigation-local policy/accepted exception
```

Each layer must declare whether a lower layer may relax one of its rules.

Core invariants are never relaxable.

## 10. Rule effect

A policy rule may produce:

- Allow
- Block
- RequireObligation
- RequireReview
- RequireEvidence
- RequirePermission
- Warn
- Annotate
- NoOpinion

Warnings do not make an illegal transition legal.

## 11. Rule composition

Rules must compose predictably.

Default precedence:

1. any core invariant Block => Blocked
2. unsupported required semantics => UnsupportedSemantics
3. aggregate version mismatch => Conflict
4. any non-relaxable policy Block => Blocked
5. unresolved required Review => RequiresReview
6. otherwise Allowed

Warnings and annotations accompany the result but do not override it.

## 12. Explicit relaxation

If a domain policy allows a later layer to relax a rule, that permission must be explicit.

Example:

- EDF policy may allow a qualified human to accept a MaterialNonBlocking unknown.
- Core policy never allows a later layer to redefine Unknown as False.

Relaxation requires:

- actor capability
- reason
- policy authority
- provenance
- event recording

## 13. Exceptions

An exception is not an invisible bypass.

When policy permits an exception, the system records:

- requested rule exception
- actor
- reason
- authority
- scope
- expiry when applicable
- affected transition
- residual risk/uncertainty
- policy version

The resulting event remains auditable.

## 14. Review

A transition may require human or organizational review.

Review requirement fields:

- ReviewRequirementId
- target command/entity
- required role/capability
- number of approvals if applicable
- independence constraint if applicable
- reason
- policy source/version
- expiry/revalidation rule

Review and execution are distinct.

## 15. Independent review

Policies may require reviewer independence.

Possible constraints:

- reviewer cannot be original proposer
- reviewer cannot be evidence author
- reviewer must belong to a different role/group
- reviewer must not share the same agent execution provenance

The core supports the concept; specific independence rules remain policy.

## 16. Capability projection

Capabilities are produced by evaluating possible commands against current state.

For each capability:

```text
Capability
- CapabilityId
- CommandKind
- State: Available | Unavailable | RequiresReview | Unsupported
- blockers
- requirements
- source policies
- actor-specific permission result
```

The UI should render this projection.

The UI must not reconstruct policy from button-specific conditionals.

## 17. Actor capability

Actor permission and domain legality are separate.

A command can be:

- semantically legal but actor-not-authorized
- actor-authorized but semantically illegal
- both legal and authorized
- requiring review despite both

This distinction should remain visible in diagnostics.

## 18. Policy identifiers

Every policy must have:

- PolicyId
- Version
- owning layer/domain
- effective status
- compatible semantic range
- rule identifiers
- change provenance

A rule should have a stable RuleId across non-semantic edits.

If a rule's meaning changes materially, its policy version changes.

## 19. Policy snapshot

Every authoritative transition records the policy references that governed it.

The event does not need to embed all rule source text if immutable policy artifacts are addressable, but enough identity must be preserved to reconstruct the decision basis later.

## 20. Policy evaluation trace

For consequential transitions, the engine should be able to emit an explainability trace.

The trace may include:

- evaluated rules
- facts/input entities used
- rule outcomes
- blockers generated
- obligations generated/resolved
- reviews checked
- final composition result

This trace is diagnostic/audit evidence and may be stored separately from the minimal semantic event.

It must not expose secrets.

## 21. Determinism

Core/domain legality must be deterministic.

An LLM may propose:

- whether a piece of evidence appears relevant
- a candidate relationship
- a potential contradiction
- a possible next action

But once represented as a command, legality is evaluated deterministically.

No policy rule may depend solely on "the model thinks this is allowed."

## 22. Derived epistemic state

Some epistemic status may be derived from relationships and policy.

Examples:

- Claim state
- Hypothesis support posture
- conclusion posture
- material blocker counts

Derived state should declare its derivation version.

If the derivation changes, historical authoritative events do not change.

## 23. Materiality

Materiality is policy-sensitive.

The core may represent:

- Blocking
- Material
- NonMaterial
- Informational

But the rule deciding whether a particular contradiction or unknown is blocking belongs to policy/domain semantics.

Materiality changes require provenance.

## 24. Cause confirmation

ConfirmCause must be treated as a protected transition.

The engine evaluates the active Cause Confirmation Gate and returns all material blockers rather than failing at the first one when practical.

This supports useful UI:

```text
Cannot confirm cause:
- contradiction C17 unresolved
- assumption A4 unverified
- alternative hypothesis H3 not addressed
```

## 25. Resolution

ResolveInvestigation is also protected.

It should expose unresolved:

- verification criteria
- blocking obligations
- residual uncertainty acceptance
- persistence conflicts
- required reviews

A successful domain decision is not complete until persistence succeeds.

## 26. Persistence boundary

Domain decision and persistence are distinct outcomes.

```text
DomainDecision = Allowed
Persistence = Pending
```

must not be presented as remotely committed authoritative state.

The client may optimistically project local state, but persistence status remains explicit.

## 27. Idempotency

Commands that may be retried through unstable networks should support an idempotency key.

If the same command/idempotency key is submitted again against the same intended transition, the system should return the prior result rather than create duplicate semantic events.

## 28. Duplicate semantic requests

Idempotency is not semantic deduplication.

Two users may independently submit logically similar observations or evidence.

The system may suggest duplication but must not silently merge authoritative records.

## 29. Concurrency

ExpectedAggregateVersion provides optimistic concurrency.

On mismatch:

- no destructive state change
- return Conflict
- preserve proposed command
- load remote changes
- allow explicit reconciliation/rebase
- re-evaluate policy against the new version

A command legal at version N is not presumed legal at N+1.

## 30. Revalidation

A pending command/proposal requires revalidation when any relevant input changed:

- aggregate version
- policy version
- required domain-pack version
- actor capabilities
- targeted evidence/unknown/contradiction
- extension semantics

## 31. Offline command queue

Offline operation may stage commands locally.

A queued command records:

- command envelope
- state version it was based on
- active semantic/policy manifest
- local timestamp
- dependency ordering

On reconnect, each command is revalidated in order.

The system must not simply replay raw writes.

## 32. Queue states

Suggested states:

- LocalOnly
- ReadyToSync
- Syncing
- Applied
- RevalidationRequired
- Conflict
- Rejected
- FailedTransport

Transport failure and domain rejection are different.

## 33. Reconciliation

When a queued command is stale:

1. fetch authoritative state
2. project it
3. check whether command intent is still meaningful
4. re-run semantic/policy validation
5. show changes/blockers if result differs
6. require explicit user action when semantics changed materially

Automated rebase is allowed only when meaning is preserved.

## 34. Policy evolution

A policy upgrade affects future decisions.

It does not rewrite past events.

Existing open investigations may:

- remain pinned to old policy
- explicitly migrate to new policy
- be reassessed under new policy
- be required by organization policy to upgrade

The selected strategy must be explicit.

## 35. Policy pinning

An investigation should be able to pin domain policy versions for reproducibility, especially research investigations.

Changing a pin is an authoritative event.

## 36. Policy comparison

For research, the engine should support evaluating the same immutable investigation snapshot under multiple policy versions without mutating it.

Result:

```text
PolicyEvaluationComparison
- source investigation/version
- P1 result/capabilities
- P2 result/capabilities
- semantic differences
- changed blockers/obligations
```

This is derived research evidence until explicitly admitted.

## 37. Experimental policy

Experimental policy must be isolated and versioned.

It must not silently become organization default.

Its outputs should be attributable to the experiment/profile.

## 38. Aegis integration

Invalid commands, invariant violations, unsupported semantics, and policy evaluation failures should map to structured Aegis faults.

Aegis fault identity should be stable enough for:

- UI explanation
- automated tests
- telemetry
- debugging
- report/audit references where appropriate

## 39. Policy implementation representation

The logical model does not require a general-purpose policy DSL.

Initial implementation may use strongly typed F# rules.

A declarative representation may be introduced later if it improves:

- inspectability
- versioning
- domain-pack composition
- non-code policy authoring

Do not build a universal rules engine before demonstrated need.

## 40. F# direction

Conceptual shape:

```fsharp
type RuleContext = {
    Investigation : Investigation
    Actor : ActorContext
    Command : Command
    SemanticManifest : InvestigationSemanticManifest
}

type RuleResult =
    | NoOpinion
    | Allow
    | Block of Blocker
    | RequireObligation of ObligationSpec
    | RequireReview of ReviewRequirement
    | Warn of Warning

type PolicyRule = RuleContext -> RuleResult list
```

The concrete implementation may optimize this, but rules should remain pure where practical.

## 41. Explainability requirement

For every unavailable consequential capability, the system should be able to answer:

- which rule blocked it?
- which state/evidence caused the rule to apply?
- what can change that condition?
- who can act?
- which version of the rule is active?

"Not allowed" alone is insufficient.

## 42. Test strategy

Each rule should have:

- positive scenario
- negative scenario
- boundary scenario
- version-change scenario when applicable
- extension interaction scenario when applicable

Protected transitions additionally require composition tests across policy layers.

## 43. Completion criterion

The policy engine is correctly separated when:

- UI can be replaced without changing legality
- AI provider can be replaced without changing legality
- GitHub can be replaced without changing legality
- EDF policy can change without changing generic core code
- old policy decisions remain explainable
- blockers are machine-readable and user-readable
- offline commands are revalidated, not blindly replayed
