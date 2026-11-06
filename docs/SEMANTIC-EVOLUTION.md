# Semantic Evolution and Research Safety

Status: normative architecture and governance requirements.

The Root-Cause Investigator must remain useful while EDF and other future domain methodologies continue to change. The platform therefore needs a stable semantic nucleus and a deliberately reversible extension model.

The design goal is not to predict every future EDF concept. It is to make new concepts cheap to introduce and cheap to remove without corrupting existing investigations or forcing a fork.

## 1. Principle: stabilize invariants, not research conclusions

The core should stabilize only concepts whose meaning is necessary to preserve investigation integrity across domains.

The platform must not promote a construct into the generic kernel merely because it is currently useful in EDF.

A useful rule:

> Put durable epistemic invariants in the core. Put evolving methodology in versioned extensions and policy.

Examples of durable core semantics:

- provenance must be preserved
- observation and inference are distinct
- unknown is not false
- proposals are not authoritative state
- blocking obligations block protected transitions
- historical state must remain explainable
- contradictory evidence cannot be silently erased
- implemented is not verified
- persistence truth must not be overstated

Examples of concepts that should initially remain outside the generic core unless independent need emerges:

- EDF metric names
- EDF-specific context coverage semantics
- EDF-specific evidence-closure rules
- topology vocabularies
- specific analytical lenses
- EDF confidence or calibration models
- EDF control-point ranking
- any construct whose empirical value is still being tested

## 2. Stability classes

Every semantic construct should have an explicit stability class.

### 2.1 CoreStable

Meaning is part of the generic platform contract.

Changing the meaning requires:

- explicit breaking-change review
- migration/compatibility plan
- semantic acceptance updates
- version change
- evidence that the old meaning can still be interpreted

### 2.2 DomainStable

Meaning is stable within a versioned domain pack but is not generic platform truth.

Examples may eventually include established EDF constructs.

### 2.3 Experimental

The construct is intentionally under evaluation.

Experimental constructs:

- must be namespaced
- must declare an owner/domain
- must declare a version
- must not become required for unrelated investigations
- must preserve source/provenance
- may be removed or revised
- must not silently alter CoreStable semantics

### 2.4 Deprecated

The construct remains interpretable for historical data but should not be produced by new work unless compatibility requires it.

### 2.5 Retired

The construct is no longer active but its historical representation remains readable or explicitly preserved as opaque extension data.

## 3. Promotion ladder

New concepts should move through an explicit promotion ladder:

```text
ResearchIdea
  -> ExperimentalExtension
  -> EvaluatedExtension
  -> DomainStable
  -> CandidateCore
  -> CoreStable
```

Promotion is not based on age or frequency of use.

### 3.1 ResearchIdea -> ExperimentalExtension

Requires:

- semantic definition
- intended problem
- scope
- versioned identifier
- validation method
- failure/retirement plan

### 3.2 ExperimentalExtension -> EvaluatedExtension

Requires:

- actual use or experiment evidence
- known failure modes
- compatibility impact understood
- no violation of core invariants

### 3.3 EvaluatedExtension -> DomainStable

Requires domain-specific evidence that the construct is useful enough to become part of the stable domain contract.

For EDF this should normally require evidence stronger than repository-internal conceptual preference when the construct makes empirical claims.

### 3.4 DomainStable -> CandidateCore

Requires evidence of usefulness across multiple domains or a demonstration that the concept is necessary to preserve generic investigation semantics.

### 3.5 CandidateCore -> CoreStable

Requires:

- cross-domain justification
- implementation evidence
- migration strategy
- semantic compatibility analysis
- acceptance scenarios
- clear evidence that extension status is insufficient

Default decision: do not promote to core.

## 4. Versioned semantic identity

A semantic construct must have an identity that is not just a display label.

Suggested identity form:

```text
namespace:name@version
```

Examples:

```text
core:unknown@1
edf:unknown-effect@1
edf:context-coverage@2
experiment:edf-topology-node@0.3
```

Display names may change without changing semantic identity.

A new meaning requires a new semantic version or identifier.

## 5. Layered type model

F# should preserve strong typing without forcing every future concept into a closed union.

Use a layered model.

### 5.1 Closed core types

Use discriminated unions for semantics that truly are CoreStable and where illegal states must remain unrepresentable.

Examples:

```fsharp
type Authority =
    | Authoritative
    | Proposal

type ProposalSource =
    | HumanProposal
    | AgentProposal

type PersistenceState =
    | Synced
    | Pending
    | Conflict
    | Failed
```

### 5.2 Extensible semantic references

For evolving classifications use qualified, versioned identifiers:

```fsharp
type SemanticNamespace = SemanticNamespace of string
type SemanticName = SemanticName of string
type SemanticVersion = SemanticVersion of string

type SemanticId =
    {
        Namespace : SemanticNamespace
        Name : SemanticName
        Version : SemanticVersion
    }
```

### 5.3 Core plus extension

Where a concept has important core cases but future domain-specific cases are expected:

```fsharp
type EvidenceRelation =
    | CoreRelation of CoreEvidenceRelation
    | ExtensionRelation of SemanticId
```

The extension payload must be validated by the domain-pack schema.

This prevents two opposite traps:

1. making everything an untyped string/JSON blob;
2. hard-coding every research construct into a closed core DU.

## 6. Extension envelopes

Domain-specific or experimental data should be stored in a versioned extension envelope.

Conceptual form:

```fsharp
type ExtensionEnvelope =
    {
        SemanticId : SemanticId
        SchemaId : string
        Payload : OpaqueExtensionPayload
        SourceVersion : string
        CreatedBy : ActorId
        CreatedAt : Instant
    }
```

The core owns the envelope, identity, provenance, round-trip guarantees, and permission boundaries.

The domain pack owns payload meaning and validation.

## 7. Unknown-extension preservation

A client that encounters an installed investigation extension it does not understand must not discard it.

Required behavior:

- preserve unknown extension data byte-for-byte or semantically losslessly where practical
- render a safe generic representation
- identify the missing extension/domain pack
- prevent writes that would require interpreting unknown semantics
- allow unrelated safe edits when they cannot damage the unknown extension
- never reinterpret an unknown extension as a familiar concept based on its label alone

This is a "tolerant reader, strict writer" rule.

## 8. Capability negotiation

An investigation should record the semantic capabilities required to edit it safely.

Example manifest:

```text
Core schema: 2
Domain pack: edf@0.4
Required extensions:
  - edf:unknown-effect@2
  - edf:evidence-closure@1
Optional extensions:
  - experiment:topology@0.3
```

On load, the client determines:

- fully understood
- readable with degraded projection
- read-only due to unknown required semantics
- migration required
- unsupported

The UI must surface that state.

## 9. Policy is versioned independently from data shape

Schema and policy are different concerns.

A domain-pack update may leave data shape unchanged but change:

- which evidence is required
- which unknowns block
- what qualifies as confirmation
- how a conclusion posture is calculated
- what verification is required

Therefore each investigation/event should preserve relevant:

- core schema version
- domain schema version
- domain policy version
- extension versions
- decision policy version

A historical transition must be judged according to the policy in force when it occurred, not retroactively against today's policy unless an explicit reassessment is being performed.

## 10. Reassessment is not history rewrite

The platform must support:

```text
HistoricalConclusion
  + NewPolicyOrEvidence
  -> Reassessment
  -> CurrentConclusion
```

It must not do:

```text
HistoricalConclusion
  -> silently mutate historical meaning
```

A reassessment should link:

- prior conclusion
- prior policy/version
- new evidence or policy
- resulting current posture
- whether corrective action or verification must be reopened

## 11. Migration strategy

Migrations must prefer non-destructive approaches.

Preferred order:

1. compatibility projection
2. upcaster/read adapter
3. additive migration
4. derived projection rebuild
5. explicit semantic migration

Destructive rewriting should be the last resort.

### 11.1 Upcasters

Historical events may be read through version-aware upcasters that produce a current in-memory projection while retaining original persisted events.

### 11.2 Side-by-side semantic migration

If a concept changes meaning materially, create the new representation alongside the old one and record a migration event linking them.

### 11.3 No fake certainty

A migration must not manufacture information that the old data never contained.

Missing information remains Unknown or unavailable.

## 12. Experimental isolation

Experimental constructs must not contaminate stable investigations accidentally.

Controls should include:

- investigation-level feature activation
- domain-pack version pinning
- explicit experimental profile
- namespaced storage
- UI indication of experimental semantics
- report indication where experimental constructs materially affect conclusions
- ability to disable an experiment for new investigations without breaking existing ones

A workspace may allow experimental features, but activation should still be recorded per affected investigation.

## 13. Feature flags versus semantic versions

A feature flag controls availability.

A semantic version controls meaning.

They must not be conflated.

Turning a feature flag off must not make persisted records uninterpretable.

## 14. Domain pack composition

A domain pack should be composed from separable modules where practical:

```text
DomainPack
  ├── Vocabulary
  ├── Semantic extensions
  ├── Policies
  ├── Validation rules
  ├── Action templates
  ├── AI guidance
  ├── UI projections
  ├── Report projections
  ├── Import/export adapters
  └── Migrations
```

A change in UI vocabulary should not require a semantic migration.

A change in report rendering should not change transition legality.

A change in policy should not silently reinterpret stored evidence.

## 15. Core invariant firewall

Extensions may strengthen requirements but cannot bypass the core invariant firewall.

An extension may not:

- erase provenance
- merge observation and inference
- treat unknown as false
- promote an agent proposal directly to evidence without the required transition
- hide a material contradiction
- mark an unverified action as verified
- pretend a failed write succeeded
- rewrite historical events
- discard extension data it does not understand

If an extension requests such behavior, loading or activating it must fail with an Aegis-compatible fault.

## 16. Experimental relationships

Relationship taxonomies are especially likely to evolve.

The generic kernel should keep only relationships needed for stable epistemic integrity in closed core types.

Domain-specific analytical relationships should be extensible through SemanticId.

A new EDF relationship therefore does not require recompiling the generic semantic model unless it demonstrates a cross-domain invariant.

## 17. Experimental metrics

Metrics such as EDF CDR, MDR, MaDR, ContextCoverage, EvidenceClosure, future calibration measures, or control-point scores should be treated as domain projections or evidence artifacts unless their semantics become generic.

The generic platform should support:

```text
MetricDefinition
MetricObservation
MetricScope
MetricVersion
MetricDerivation
MetricEvidence
```

without understanding the metric's domain meaning.

This makes future EDF metric replacement cheap.

## 18. Research claims inside a domain pack

A domain pack may contain claims whose maturity differs.

Suggested maturity:

- Hypothesized
- Experimental
- ProvisionallySupported
- DomainAccepted
- Deprecated
- Rejected

A DomainAccepted construct is not automatically CoreStable.

The product should be able to show an EDF concept as experimental even while the core platform itself is stable.

## 19. Research registries

Domain packs under active research should be able to import or reference registries for:

- hypotheses
- evidence
- theories
- experiments
- decisions
- frontier questions

The investigator should not hard-code the current layout of those registries.

Adapters map external registry records to generic investigation primitives.

## 20. EDF-specific research safety

EDF is currently an active research program. The EDF pack must therefore assume change.

Until EDF evidence justifies stronger stability, concepts such as the following should be isolated behind versioned EDF semantics rather than promoted into the generic kernel:

- ContextCoverage
- EvidenceClosure
- UnknownEffect specialization
- topology/layer constructs
- control-point ranking
- lens sets
- EDF-specific confidence/calibration representations
- EDF metric definitions
- any future evidence relation unique to EDF

The EDF pack should be capable of representing that an EDF concept itself is under investigation.

## 21. Recursive EDF use

The platform should support investigating its own domain pack.

Example:

```text
Observation:
EDF ContextCoverage changed between v0.3 and experimental v1 behavior.

Hypotheses:
- new scope semantics improve discrimination
- new scope semantics over-reject useful evidence
- metric implementation changed without intended semantic change

Evidence:
comparative experiment outputs

Action:
run matched cases under both policies

Conclusion:
current evidence favors one semantic version, with residual uncertainty
```

The resulting evidence may justify promoting, revising, or rejecting the EDF construct.

This recursive use must not give the EDF pack special authority over the generic kernel.

## 22. Data ownership boundaries

Core data must remain interpretable without EDF.

EDF extension data may depend on EDF.

The canonical export should therefore separate:

- core investigation state
- domain-pack manifest
- extension envelopes
- policy/version manifest
- external artifact references

A generic client should be able to inspect the core investigation even when EDF-specific projections are unavailable.

## 23. Generic fallback rendering

Every extension should have a safe fallback representation containing at least:

- semantic identifier
- display label when available
- stability class
- source/domain
- version
- raw or summarized value where safe
- provenance
- indication that specialized interpretation is unavailable

Critical extension information must never simply disappear because a specialized UI component is missing.

## 24. AI contract composition

Agent context should be assembled at runtime from:

1. generic investigation semantics
2. core policy
3. active domain pack
4. domain policy version
5. explicitly enabled experimental semantics
6. investigation-specific state
7. authorized tools

The agent must not assume every concept seen in historical EDF material is active in the current domain-pack version.

## 25. AI output version binding

When an agent proposes domain-specific state, the proposal must identify the semantic/policy version under which it was generated.

If the active domain pack changes before acceptance, the proposal may require revalidation.

## 26. Extension compatibility testing

Every domain-pack/extension release should test:

- can current data be read?
- can unknown optional extensions round-trip?
- do required unknown extensions correctly force degraded/read-only mode?
- do old events project consistently?
- do policy transitions remain attributable?
- do retired constructs remain inspectable?
- does generic fallback rendering preserve meaning?
- does disabling experimental functionality preserve existing data?
- can exports be re-imported?
- do core invariant tests still pass unchanged?

## 27. Research rollback

Every experimental semantic addition must define rollback behavior before activation.

Rollback must answer:

- what happens to existing records?
- can they remain visible?
- can they remain read-only?
- are they translated to another concept?
- does any conclusion depend on them?
- must affected conclusions be reassessed?

"No longer use this field" is not a sufficient rollback strategy.

## 28. Deprecation

Deprecation should be explicit and versioned.

A deprecated construct must define:

- replacement, if any
- last version allowed for new writes
- read-support duration/policy
- migration guidance
- report/UI treatment
- effect on historical conclusions

## 29. Fork prevention test

Before adding anything to the generic kernel, ask:

1. Is this necessary to preserve cross-domain epistemic integrity?
2. Would a versioned extension work?
3. Is the concept still under active research?
4. Can we imagine another domain disagreeing with its semantics?
5. Would changing it require rewriting old investigations?
6. Does putting it in core force every deployment to understand it?
7. Is there evidence that it is stable enough to deserve a closed type?

If the answers point toward uncertainty, keep it out of core.

## 30. Architecture fitness tests

The architecture should continuously prove the following:

### F1. Add without core change
A new EDF experimental construct can be added without changing generic kernel source.

### F2. Remove without data loss
An experimental construct can be disabled for new work while historical investigations remain readable.

### F3. Revise meaning safely
A material EDF semantic change can introduce a new version without reinterpreting old records.

### F4. Unknown extension survival
A generic client can load, preserve, and re-save unrelated state without dropping an optional EDF extension it does not understand.

### F5. Required extension protection
A client that lacks a required extension does not permit semantic writes that could corrupt the investigation.

### F6. Policy evolution
A new confirmation policy does not retroactively alter why an old transition was legal.

### F7. Reassessment
An old conclusion can be reevaluated under a newer policy while the original conclusion remains historical fact.

### F8. No EDF fork
An EDF-specific investigation remains a generic investigation plus an EDF manifest/extensions, not a different persistence model.

### F9. Core invariant permanence
All core invariant tests run unchanged against every domain pack and experimental profile.

### F10. Metric replacement
An EDF metric can be replaced or redefined under a new semantic identity without requiring a core schema change.

## 31. Default posture

When uncertain whether a construct belongs in core or in a domain extension:

> keep it in the extension.

Moving a validated concept inward later is manageable.

Extracting an unstable concept from the core after persisted investigations depend on it is much more expensive and risks historical reinterpretation.
