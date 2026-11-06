# Domain Pack Contract

Status: normative extension architecture direction.

A Domain Pack adds specialized investigation semantics without forking or redefining the generic investigation platform.

This document defines the logical contract. It does not require packs to be dynamically downloaded executable plugins. A pack may be compiled into the application, loaded declaratively, or hosted by a service, provided the same semantic contract is preserved.

## 1. Goals

A Domain Pack must allow a domain to add:

- terminology
- semantic extensions
- policy
- validation rules
- investigation templates
- action templates
- evidence classifications
- metrics and derived projections
- AI guidance
- UI projections
- report projections
- import/export adapters
- migration/upcast support

without modifying the generic investigation kernel.

## 2. Non-goals

A Domain Pack is not:

- an alternate persistence model
- a bypass around core invariants
- arbitrary trusted code with unrestricted host access
- permission to redefine generic semantics
- a fork of Investigation
- a second event system
- a second source of authoritative history

## 3. Manifest

Every pack requires a versioned manifest.

Conceptual fields:

```text
DomainPackManifest
- PackId
- Version
- DisplayName
- Stability
- CompatibleCoreRange
- SemanticDefinitions
- RequiredDependencies
- OptionalDependencies
- PolicySets
- FeatureDefinitions
- ExtensionSchemas
- MigrationDefinitions
- UIProjectionDefinitions
- ReportProjectionDefinitions
- AIInstructionDefinitions
- ImportExportDefinitions
- Provenance
```

PackId must be stable across versions.

## 4. Compatibility

A pack must declare the compatible generic core range.

The host must reject activation when:

- the required core version is unsupported
- a required semantic dependency is unavailable
- a required migration cannot be performed
- the pack requests forbidden core-invariant overrides

Compatibility cannot be inferred from version numbers alone when semantics differ.

## 5. Semantic definitions

Every extension semantic should declare:

- SemanticId
- display name
- definition
- owning pack
- stability class
- applicable core entity kinds
- schema
- validation rules
- requiredness rules
- supersedes/superseded-by relationships
- deprecation information
- generic fallback metadata

The definition is machine-readable where practical and human-readable always.

## 6. Schema ownership

The pack owns validation of its extension payloads.

The core owns validation of:

- envelope structure
- semantic identity
- manifest consistency
- provenance presence
- extension requiredness
- invariant firewall
- compatibility state

## 7. Policy sets

Policy is versioned independently.

A PolicySet may define:

- transition prerequisites
- obligation generation
- blocker classification
- review requirements
- conclusion confirmation gates
- resolution gates
- evidence requirements
- assumption acceptance
- unknown acceptance
- verification requirements
- role/permission requirements

A policy rule may strengthen generic requirements.

It may not waive a CoreStable invariant.

## 8. Policy evaluation

The core transition engine should compose policy in deterministic layers:

```text
Core invariant firewall
    ↓
Core policy
    ↓
Domain pack policy
    ↓
Experimental profile policy
    ↓
Organization policy
    ↓
Investigation-specific accepted exception policy, if permitted
```

Later layers may add blockers or review requirements.

They cannot erase a blocker imposed by the core invariant firewall.

Whether organization policy may relax a domain-pack requirement must be explicit in that domain policy's contract.

## 9. Feature definitions

A pack feature should declare:

- FeatureId
- semantic effect
- default state
- stability
- activation scope
- dependencies
- incompatibilities
- rollback behavior
- data written when enabled

Feature availability and semantic version are separate.

## 10. Experimental profiles

An ExperimentalProfile is an explicit composition of experimental pack features and policies.

It must declare:

- profile id/version
- purpose/hypothesis
- enabled semantics
- changed policies
- expected observations
- validation criteria
- rollback behavior

An investigation that uses it records the profile identity.

## 11. Metric definitions

A MetricDefinition should be domain-owned.

Conceptual metadata:

- Metric SemanticId
- name
- definition
- unit
- derivation/version
- required input semantics
- scope rules
- known limitations
- stability class

Metric observations are evidence/derived projections with provenance.

Changing a metric's meaning requires a new semantic version.

## 12. Derived projections

A pack may calculate derived views.

Examples:

- EDF metric dashboard
- diagnostic topology
- evidence-closure projection
- control-point ranking
- domain conclusion summary

Derived projections are disposable unless explicitly admitted as Evidence with provenance.

They must be reproducible from authoritative inputs or clearly labeled as non-reproducible analysis.

## 13. AI guidance

A pack may contribute:

- terminology
- domain questions
- hypothesis prompts
- action templates
- interpretation rules
- tool descriptions
- warnings
- examples
- prohibited domain behaviors

Pack AI guidance is subordinate to:

- system safety
- core semantic invariants
- current policy
- authoritative investigation state

AI guidance may suggest a state change but cannot create legal authority.

## 14. AI structured output

Pack-specific agent output should use versioned semantic schemas.

The host must validate:

1. output syntax
2. semantic identity/version
3. pack schema
4. core invariants
5. command legality

before authoritative acceptance.

## 15. UI projection

A pack may contribute specialized rendering for its extension semantics.

The host still owns:

- global accessibility
- navigation integrity
- persistence status
- generic legal-action behavior
- generic fallback rendering
- core error/fault presentation

A pack UI must not hide generic blockers or contradictions.

## 16. Report projection

A pack may add domain report sections.

Every specialized report section should identify the semantic/version context needed to interpret it when material.

Core report sections around provenance, uncertainty, authoritative conclusions, and verification cannot be silently removed.

## 17. Import adapters

A pack may map external domain artifacts into core or extension semantics.

An adapter must identify:

- source system/type
- source version
- mapping version
- imported provenance
- whether fields are direct observations, evidence, claims, or interpretations
- unsupported/unmapped source content

Unmapped data must not be invented.

## 18. Export adapters

Domain-specific export may add specialized representations.

Canonical export must still preserve:

- generic core state
- pack manifest
- semantic manifests
- extension envelopes
- policy/version information

## 19. Migration

A pack release must identify any change affecting persisted semantics.

Migration classes:

- None
- ProjectionOnly
- Additive
- Upcast
- SemanticSideBySide
- ExplicitBreakingMigration

SemanticSideBySide is preferred when old and new meanings differ materially.

## 20. Upcasters

An upcaster may adapt historical shape for current in-memory interpretation.

An upcaster must not:

- invent unrecorded facts
- alter original persisted provenance
- make an old record appear to have used a concept introduced later
- erase deprecated fields required to understand history

## 21. Extension code execution

The logical contract must not assume arbitrary extension code is safe.

If a pack includes executable extension logic:

- it must use explicit host capabilities
- it receives only authorized data
- it must not access secrets implicitly
- failures must be isolated
- time/resource limits should be enforceable where practical
- it must not mutate authoritative state except through validated commands

For the hackathon, compile-time composition is acceptable and may be safer than dynamic code loading.

## 22. Security

A pack must declare required external capabilities.

Examples:

- read external evidence source
- invoke AI tool
- publish integration event
- render external artifact metadata

Capabilities require host authorization.

A pack cannot grant itself broader permissions by changing its manifest at runtime.

## 23. Faults

Pack failures must use Aegis-compatible structured faults.

Faults should identify:

- pack id/version
- semantic id when relevant
- operation
- recoverability
- affected investigation/entity
- user-safe explanation
- diagnostic correlation

A pack failure must not corrupt unrelated core state.

## 24. Observability

The host should be able to attribute:

- policy blockers
- projection failures
- AI schema validation failures
- migrations
- pack load failures
- extension validation failures

to the responsible pack/version.

## 25. Testing contract

Every stable pack release should prove:

- core invariant suite passes
- pack manifest validates
- required dependencies resolve
- extension schemas validate
- legal/illegal transitions match policy
- migrations preserve historical meaning
- generic fallback works
- unknown extension behavior works
- accessibility obligations still hold
- canonical export/import round-trips
- unsupported versions fail safely

Experimental packs may have a reduced release gate only when explicitly marked and isolated.

## 26. EDF pack posture

EDF should begin as a pack with a deliberately conservative stable surface.

The initial pack should prefer:

- generic core records
- versioned EDF metadata
- versioned EDF metrics
- EDF-specific policies
- EDF-specific report/UI projections

over new generic entity types.

New EDF constructs should start Experimental unless the current EDF evidence boundary supports DomainStable classification.

## 27. Pack comparison

The platform should eventually support running two pack or policy versions over the same immutable evidence snapshot for research comparison.

Example:

```text
Investigation evidence snapshot V20
  -> EDF v0.3 policy projection
  -> EDF experimental v1 policy projection
```

The outputs are separate derived/reassessment artifacts.

Neither silently overwrites the other.

## 28. Pack removal

Removing a pack from a deployment must not delete its historical extension data.

Possible resulting modes:

- full specialized access
- generic fallback access
- read-only due to required semantics
- export-only recovery

The host must state which mode applies.

## 29. Pack promotion

When an experimental feature becomes DomainStable:

- semantic identity may remain the same if meaning did not change
- stability classification changes with provenance
- acceptance tests become required
- migration is unnecessary solely because maturity changed

If meaning changed during promotion, a new semantic version is required.

## 30. Core promotion

A domain construct promoted into generic core requires an explicit adapter so existing extension records remain interpretable.

Promotion must not require pretending historical extension records were originally core records.

## 31. Contract fitness test

The contract is successful if EDF can:

- add a new research construct
- run experiments using it
- compare it with an older construct
- reject it
- remove it from new investigations
- keep old investigations readable

without modifying the generic Investigation persistence model or rewriting historical state.
