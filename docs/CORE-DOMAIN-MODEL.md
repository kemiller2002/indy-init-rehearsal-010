# Minimal Core Domain Model

Status: normative architecture direction, pre-implementation.

This document translates the canonical investigation semantics into the smallest useful domain model that can remain stable while EDF and other domain packs evolve.

It intentionally describes semantic shape and aggregate boundaries rather than committing to a final serialization format.

## 1. Design rule

The core stores durable investigation meaning.

Domain packs store specialized meaning.

Projections store convenient views.

The persistence format must not confuse those three.

## 2. Aggregate boundary

The primary aggregate is Investigation.

Conceptual shape:

```text
Investigation
  ├── Identity and lifecycle
  ├── Version manifest
  ├── Core semantic records
  ├── Proposals
  ├── Relationships
  ├── Obligations
  ├── Capabilities
  ├── Extension envelopes
  ├── Event/history references
  └── Persistence state
```

An Investigation is the transactional semantic boundary for legal state changes.

Portfolio/workspace metadata should not be required to interpret the investigation itself.

## 3. Investigation identity

Core fields:

- InvestigationId
- title
- creation provenance
- lifecycle state
- current aggregate version
- semantic/version manifest
- active domain-pack references
- current policy references
- created timestamp
- last material state-change timestamp

InvestigationId must remain stable across:

- storage providers
- repository moves
- import/export
- migrations
- domain-pack upgrades

## 4. Semantic version manifest

Every authoritative investigation snapshot must identify how to interpret it.

Conceptual model:

```fsharp
type Requiredness =
    | Required
    | Optional

type SemanticDependency =
    {
        Id : SemanticId
        Requiredness : Requiredness
    }

type InvestigationSemanticManifest =
    {
        CoreSchemaVersion : string
        CoreSemanticVersion : string
        DomainPacks : DomainPackRef list
        PolicyRefs : PolicyRef list
        SemanticDependencies : SemanticDependency list
    }
```

The manifest is part of authoritative state, not deployment documentation.

## 5. Core entity identifiers

Every material entity needs a stable typed identifier.

Examples:

- ObservationId
- ClaimId
- InterpretationId
- HypothesisId
- AssumptionId
- EvidenceId
- PredictionId
- InvestigationActionId
- FindingId
- UnknownId
- NegativeKnowledgeId
- ContradictionId
- ObligationId
- CausalAssertionId
- CorrectiveActionId
- VerificationId
- ConclusionId
- ProposalId
- EventId

Typed IDs reduce accidental cross-linking.

Serialization may use a common opaque ID representation while the F# domain preserves type distinctions.

## 6. Entity reference

Cross-cutting graphs need a safe generic reference.

Conceptual model:

```fsharp
type CoreEntityKind =
    | ObservationEntity
    | ClaimEntity
    | InterpretationEntity
    | HypothesisEntity
    | AssumptionEntity
    | EvidenceEntity
    | PredictionEntity
    | InvestigationActionEntity
    | FindingEntity
    | UnknownEntity
    | NegativeKnowledgeEntity
    | ContradictionEntity
    | ObligationEntity
    | CausalAssertionEntity
    | CorrectiveActionEntity
    | VerificationEntity
    | ConclusionEntity

type EntityKind =
    | CoreEntity of CoreEntityKind
    | ExtensionEntity of SemanticId

type EntityRef =
    {
        Kind : EntityKind
        Id : string
    }
```

ExtensionEntity allows a future domain pack to introduce a real specialized record without adding another case to the generic core union.

## 7. Scope

Scope is essential because many statements are true only for a particular time, version, environment, population, execution, or subsystem.

The core should provide common scope dimensions but remain extensible.

Conceptual shape:

```fsharp
type Scope =
    {
        Time : TimeScope option
        Systems : string list
        Versions : string list
        Environments : string list
        Subjects : string list
        ExtensionScopes : ExtensionEnvelope list
    }
```

Empty scope must not automatically mean universal scope.

Policy should define how unspecified scope is interpreted.

## 8. Provenance

All material records should use a common provenance structure.

Conceptual fields:

- Actor
- created time
- source reference
- ingestion/import reference
- transformation chain
- agent/provider/model metadata when relevant
- semantic/policy version
- supersession lineage
- verification information

Provenance must be additive. A transformation adds provenance; it does not replace origin provenance.

## 9. Observation

Minimal semantic fields:

```text
Observation
- id
- original content/source representation
- normalized summary optional
- source kind
- scope
- observed-at optional
- recorded-at
- provenance
- verification state
- extensions
```

Normalization must never replace original content.

## 10. Claim

Minimal fields:

```text
Claim
- id
- proposition
- scope
- epistemic state
- provenance
- relationship references
- extensions
```

The proposition may later become more structured, but the core should not require a universal domain proposition AST before evidence demonstrates the need.

## 11. Interpretation

Minimal fields:

```text
Interpretation
- id
- statement
- source entity references
- scope
- provenance
- extensions
```

An Interpretation must have at least one reason/source reference unless it is explicitly recorded as an unsupported proposal.

## 12. Hypothesis

Minimal fields:

```text
Hypothesis
- id
- explanatory claim/reference
- what it explains
- lifecycle state
- mechanism reference optional
- assumptions
- predictions
- supporting relationships
- contradictory relationships
- unknown references
- scope
- provenance
- extensions
```

Do not put EDF analytical lenses directly into Hypothesis.

A domain pack may attach those through extensions.

## 13. Assumption

Minimal fields:

- id
- underlying Claim reference or proposition
- assumption lifecycle state
- scope
- materiality/blocking classification
- dependent entity references
- provenance
- extensions

## 14. Evidence

Minimal fields:

```text
Evidence
- id
- content or artifact reference
- evidence category
- scope
- source
- provenance
- verification status
- quality dimensions
- dependency references
- extensions
```

EvidenceCategory should support core categories plus SemanticId extension categories.

Evidence quality is multidimensional and should not require one universal score.

## 15. Evidence relationship

Minimal fields:

- relationship id
- EvidenceId
- target EntityRef
- CoreRelationship or ExtensionRelationship
- scope
- rationale
- provenance
- status
- extensions

Relationship state may itself be challenged or superseded.

## 16. Prediction

Minimal fields:

- id
- originating hypothesis references
- prediction type
- expected observable condition
- alternative predictions
- prospective/retrospective status
- scope
- created-before-result evidence
- provenance
- extensions

## 17. Investigation action

Minimal fields:

- id
- action kind
- purpose
- target entities
- predictions
- preconditions
- lifecycle state
- cost/risk metadata
- execution references
- provenance
- extensions

ActionKind should support generic core kinds and extension kinds.

## 18. Finding

Minimal fields:

- id
- source InvestigationAction
- original result
- normalized result optional
- scope
- anomalies
- provenance
- admitted-as-evidence references
- extensions

Finding and Evidence remain distinct so raw result capture does not automatically imply interpretation.

## 19. Unknown

Minimal fields:

- id
- question/proposition whose value is unknown
- lifecycle state
- blocking/materiality state
- scope
- dependent entities
- resolution references
- provenance
- extensions

Domain-specific unknown types belong in extensions.

## 20. Negative knowledge

Minimal fields:

- id
- ruled-out entity/claim/path
- negative-knowledge kind
- reason
- evidence basis
- scope
- status
- provenance
- extensions

NegativeKnowledgeKind may include core cases and domain extensions.

## 21. Contradiction

Minimal fields:

- id
- participating entity references
- contradiction statement
- materiality
- lifecycle state
- scope
- resolution basis
- provenance
- extensions

The contradiction record should survive resolution.

## 22. Obligation

Minimal fields:

- id
- reason
- policy/rule source
- target transition/entity
- blocking level
- lifecycle state
- owner optional
- due metadata optional
- resolution evidence
- provenance
- extensions

Obligations are generated from policy and state, but accepted obligations should remain traceable even if policy later changes.

## 23. Causal assertion

The core should avoid assuming every domain uses a single-root tree.

Use CausalAssertion as the stable semantic record.

Conceptual fields:

- id
- target outcome/entity
- cause member references
- causal role/classification
- mechanism reference optional
- causal-support dimensions
- scope
- lifecycle/conclusion status
- alternative explanation references
- provenance
- extensions

A CausalAssertion may reference one cause member or a causal set.

Domain packs may extend causal roles without changing the core storage model.

## 24. Corrective action

Minimal fields:

- id
- action classification
- target cause/mechanism
- lifecycle
- owner
- implementation evidence
- verification criteria
- verification references
- provenance
- extensions

Classification should distinguish generic mitigation/workaround/correction/prevention and allow domain extensions.

## 25. Verification

Minimal fields:

- id
- target action/criterion
- criterion reference
- finding/evidence
- outcome
- scope
- provenance
- extensions

## 26. Conclusion

Minimal fields:

- id
- conclusion statement
- conclusion strength/posture
- causal assertion references
- evidence basis
- unresolved contradictions
- residual unknowns
- accepted assumptions
- policy/version
- provenance
- extensions

A Conclusion is a snapshot of justified posture, not a replacement for the underlying graph.

## 27. Proposals

Proposals should use a common envelope rather than duplicate the entire authoritative model.

Conceptual form:

```fsharp
type Proposal<'T> =
    {
        Id : ProposalId
        ProposedValue : 'T
        ProposedBy : Actor
        ProposedAt : Instant
        SemanticManifest : InvestigationSemanticManifest
        Rationale : string option
        State : ProposalState
    }
```

Acceptance must create the appropriate authoritative command/event.

The proposal itself remains historical provenance.

## 28. Extension attachment

Every core entity should be extensible without adding fields to its core record for each domain.

Preferred logical contract:

```text
EntityRef
  -> zero or more ExtensionEnvelope
```

An implementation may physically colocate extension envelopes for efficiency, but their semantics remain separate.

## 29. Extension envelope

Required fields:

- SemanticId
- schema identifier/version
- stability class
- payload
- provenance
- required/optional semantics
- domain-pack owner

The core validates envelope mechanics.

The owning domain pack validates payload meaning.

## 30. Commands

Commands request authoritative state changes.

Examples:

- StartInvestigation
- RecordObservation
- AddClaim
- ProposeHypothesis
- AcceptHypothesis
- AdmitEvidence
- LinkEvidence
- IdentifyUnknown
- RecordAssumption
- ProposeInvestigationAction
- RecordFinding
- EstablishNegativeKnowledge
- RecordContradiction
- ResolveContradiction
- ProposeCause
- ConfirmCause
- ProposeCorrectiveAction
- MarkCorrectiveActionImplemented
- RecordVerification
- ResolveInvestigation
- ReopenInvestigation
- AttachExtension
- MigrateSemanticExtension
- ReassessConclusion

Commands should carry expected aggregate version for optimistic concurrency.

## 31. Command decision

The domain engine evaluates a command against:

1. core invariants
2. current authoritative state
3. active core policy
4. active domain policies
5. required extensions
6. permissions/capabilities
7. expected aggregate version

Conceptual result:

```fsharp
type Decision<'event> =
    | Allowed of 'event list
    | Blocked of Blocker list
    | RequiresReview of ReviewRequirement
    | UnsupportedSemantics of SemanticDependency list
    | Conflict of expectedVersion:int64 * actualVersion:int64
```

Blocked reasons are domain output and should drive the UI.

## 32. Events

Accepted commands emit immutable semantic events.

Events must record:

- EventId
- InvestigationId
- sequence/aggregate version
- event kind
- actor
- timestamp
- semantic manifest
- policy refs
- payload
- command/request correlation
- evidence references when required

Events should be narrow statements about what happened, not full mutable snapshots.

## 33. Projection

Current state is a projection of authoritative events plus migration/upcast logic.

Projection must be deterministic for the same:

- event stream
- schema versions
- domain-pack versions
- policy-independent projection rules

Policy may determine legal next actions, but should not rewrite what past events mean.

## 34. Snapshots

Snapshots are performance artifacts.

A snapshot must identify:

- source event/version
- semantic manifest
- projection version

A corrupt or stale snapshot must be disposable and reconstructable from authoritative data when the selected persistence provider supports event reconstruction.

## 35. Capability projection

Legal capabilities are domain projections.

Conceptual form:

```fsharp
type CapabilityState =
    | Available
    | Unavailable of Blocker list
    | RequiresReview of ReviewRequirement
    | UnsupportedDueToSemantics of SemanticDependency list
```

The UI must consume capabilities instead of duplicating transition rules.

## 36. Persistence independence

The aggregate must not know:

- GitHub commit SHA layout
- SQL table names
- HTTP endpoint paths
- local IndexedDB keys

Persistence adapters translate between storage-specific concurrency/version metadata and the investigation aggregate contract.

## 37. Search independence

Portfolio search is not part of the aggregate.

Storage/index services may build searchable projections from investigations and events.

A search index is disposable derived state and cannot become the authoritative source of investigation truth.

## 38. Report independence

Folio/report projections consume authoritative state.

Report-specific display models must not be persisted back as authoritative investigation semantics.

## 39. AI independence

The AI layer consumes a scoped projection and emits proposals/requests.

It must not own:

- lifecycle state
- transition legality
- semantic migration
- persistence truth
- policy authority

## 40. Minimal core test

A future domain pack should be implementable without changing this file's entity list when it only adds:

- specialized labels
- subtypes
- analytical relationships
- metrics
- scoring
- domain actions
- domain policy
- domain UI
- domain report sections
- domain AI guidance

If adding a domain requires changing the generic aggregate for every new research concept, the boundary has failed.


## 41. Concrete implementation contracts

The first implementation profile is further specified by:

- [COMMAND-EVENT-CATALOG.md](COMMAND-EVENT-CATALOG.md)
- [STATE-TRANSITION-TABLES.md](STATE-TRANSITION-TABLES.md)
- [FSHARP-TYPE-SHAPES.md](FSHARP-TYPE-SHAPES.md)

These documents intentionally keep comparative posture such as Leading derived and place causal confirmation on CausalAssertion rather than HypothesisState.

If empirical work later changes those choices, update the implementation profile, open-question register, acceptance scenarios, and migration analysis explicitly rather than silently changing persisted meaning.
