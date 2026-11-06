# Design Decision Boundaries

Status: normative planning control.

This document prevents provisional architecture and active research from hardening accidentally into permanent product constraints.

Every consequential design decision should be treated as one of:

- LockedInvariant
- StableDirection
- ReversibleChoice
- Experimental
- Deferred

Agents and implementers must not promote a ReversibleChoice, Experimental item, or Deferred question into a LockedInvariant without explicit evidence and a recorded decision.

## 1. LockedInvariant

These are foundational product guarantees.

### D-001 Observation/inference separation
Direct/report observations, interpretations, hypotheses, and conclusions remain semantically distinct.

### D-002 Provenance preservation
Material authoritative state retains provenance.

### D-003 Unknown semantics
Unknown is not false, disproven, or absent.

### D-004 Proposal authority
Human/agent proposals do not become authoritative merely because they were generated.

### D-005 Historical integrity
New policy, schema, or domain versions do not silently rewrite historical meaning.

### D-006 Explicit transition legality
Consequential state transitions are validated by domain rules/capabilities.

### D-007 Blocking obligations
A blocking obligation prevents the transition it protects.

### D-008 Verification distinction
Implementation/action completion is distinct from verification of outcome.

### D-009 Persistence truth
Pending, conflicted, or failed persistence is not represented as successful authoritative remote state.

### D-010 Extension invariant firewall
A domain pack or experimental profile cannot weaken core epistemic/state-integrity invariants.

### D-011 Extension preservation
Unknown optional extension semantics are preserved rather than silently discarded.

### D-012 Safe unsupported semantics
Required semantics that are not understood force degraded/read-only behavior where writes would be unsafe.

### D-013 Domain-neutral core
EDF terminology and current EDF research constructs do not become mandatory generic-core vocabulary.

### D-014 Storage abstraction
Generic investigation semantics do not depend directly on GitHub storage semantics.

### D-015 AI provider abstraction
Authoritative domain semantics do not depend on one model/provider.

## 2. StableDirection

These are strongly preferred architectural directions, but details may evolve without changing the product promise.

### D-101 F# typed domain
Use F# domain types to make invalid generic-core state difficult or impossible to represent.

### D-102 Layered type system
Use closed types for stable semantics and qualified versioned extension semantics for evolving classifications.

### D-103 Command/decision/event architecture
Consequential writes should enter through commands, return explicit decisions/blockers, and produce auditable semantic events.

### D-104 Derived capabilities
UI legal actions should be derived from domain capability projection rather than reimplemented in the client.

### D-105 Current projection plus history
Normal rendering should use current projected state while authoritative history remains available.

### D-106 Domain packs
Domain specialization should compose onto the generic kernel rather than fork it.

### D-107 Declarative-first extensions
Prefer declarative schemas/policy/projections over unrestricted executable plugin logic.

### D-108 Generic fallback rendering
Domain extensions should remain inspectable when specialized UI is unavailable.

### D-109 Runtime AI context composition
Agent context should be composed from active semantic and policy versions.

### D-110 Reassessment over reinterpretation
New evidence or methodology should produce reassessment rather than historical mutation.

## 3. ReversibleChoice

These choices may be selected for the hackathon or first production slice, but the architecture must not rely on them being permanent.

### D-201 GitHub persistence
GitHub is the initial persistence provider.

It is not the domain database abstraction.

### D-202 Repository file layout
The exact JSON/NDJSON/file layout may change.

Stable identifiers and canonical export semantics matter more than path layout.

### D-203 Static hosting
GitHub Pages/static hosting is a target where practical.

The product may later use service-backed deployment without changing domain semantics.

Because static hosting cannot answer arbitrary paths, routes are hash-based (`index.html#/...`), with relative links and no `<base href>`. A reloaded or pasted deep link therefore never 404s (R9.7, P34.18; Limen DF-LIMEN-2026-0006; Praxis SAF-URL-6). A later service-backed deployment may move to path routes only if it keeps every hash link working through legacy redirects.

### D-204 Local browser durability mechanism
IndexedDB or another browser store may be used.

The domain model must not depend on its API.

### D-205 Event storage representation
Semantic events are required conceptually.

The exact physical event-store format is not locked.

### D-206 Snapshot strategy
The exact frequency, shape, and storage of snapshots is reversible.

### D-207 Pack packaging
A domain pack may initially be compiled into the application.

Dynamic loading is not required for the first version.

### D-208 Policy implementation language
Policies may initially be F# code, typed configuration, or a hybrid.

The semantic policy/version boundary is locked; the representation is not.

### D-209 Serialization format
JSON is likely for portability, but exact serialization technology is not a core semantic decision.

### D-210 Authentication implementation
GitHub App/OAuth is preferred for GitHub deployments, but auth architecture may evolve with deployment mode.

### D-211 Search implementation
GitHub search, local indexes, service indexes, or databases may be used.

Search remains derived state.

### D-212 AI transport/backend
Direct model calls, a small service, or managed AI gateways may change without affecting domain semantics.

## 4. Experimental

These ideas may be valuable, but they must remain replaceable until evidence supports promotion.

### D-301 EDF ContextCoverage semantics
Keep versioned in EDF.

### D-302 EDF EvidenceClosure semantics
Keep versioned in EDF.

### D-303 EDF UnknownEffect specialization
Use the generic Unknown primitive plus EDF extension until evidence justifies stronger semantics.

### D-304 EDF topology/layer model
Do not hard-code into generic causal representation.

### D-305 EDF control-point ranking
Treat as a domain-specific derived projection/decision aid.

### D-306 EDF lens sets
Treat as domain policy/playbook/configuration.

### D-307 EDF metric formulas
Version each metric definition independently.

### D-308 Confidence calibration model
Do not commit the generic platform to a numeric confidence formula.

### D-309 Expected information-value scoring
The generic platform may explain/prioritize discriminating actions, but exact scoring models remain experimental.

### D-310 Automated causal inference
Do not equate statistical/graph causal methods with generic cause confirmation until their role is validated.

### D-311 Automatic hypothesis generation strategy
Prompts, agents, search strategies, and model ensembles remain experimental AI behavior.

### D-312 Automated evidence quality scoring
Evidence dimensions are stable direction; algorithmic scoring/aggregation remains experimental.

## 5. Deferred

These are intentionally not solved yet.

### D-401 Universal proposition AST
Do not build a universal formal language for every Claim/Hypothesis before demonstrated need.

### D-402 General knowledge graph
Do not require all investigation state to become one universal graph database model.

### D-403 Universal causal DAG
The core must support causal sets and relationships, but a formal DAG ontology is deferred.

### D-404 Universal probability model
Bayesian or other probability semantics may later be added as domain/analytical extensions.

### D-405 Dynamic third-party plugin marketplace
Not required for initial product architecture.

### D-406 Remote code execution for packs
Avoid until a clear security model exists.

### D-407 Pack cryptographic signing distribution system
Potentially valuable later, but not required to define domain semantics now.

### D-408 Multi-tenant SaaS architecture
The product may support it later. Do not couple the core to tenancy now.

### D-409 Enterprise RBAC taxonomy
Core needs actor/permission boundaries; exact enterprise role taxonomy is deferred.

### D-410 Universal workflow engine
Do not turn the investigation platform into a general BPM/workflow engine.

### D-411 Automated remediation execution
Corrective-action tracking is core; automatically executing arbitrary remediation is a later integration concern.

### D-412 Full ontology mapping between domains
Cross-domain learning can begin with explicit adapters and shared primitives.

A universal ontology is deferred.

## 6. Change rule

Changing a classification requires a recorded decision with:

- decision id
- old classification
- new classification
- evidence/rationale
- affected semantics
- compatibility impact
- migration impact
- acceptance-test impact
- effective version

## 7. Promotion test

Before promoting Experimental or Deferred work, ask:

1. What evidence says we need it now?
2. Is the need generic or only domain-specific?
3. Can an extension solve it?
4. What historical data would become dependent on it?
5. Can we reverse it?
6. What happens if the research conclusion changes next month?
7. Does it improve epistemic integrity, or merely implementation convenience?

Implementation convenience alone is not enough reason to harden a semantic decision.

## 8. Agent rule

Agents implementing the application must consult this document before making architecture-level assumptions.

When an unspecified decision is required during implementation:

1. prefer the most reversible option;
2. do not promote domain research into generic core;
3. record the choice as a ReversibleChoice unless an existing invariant determines it;
4. preserve compatibility seams;
5. create an explicit open decision if the choice would be difficult to undo.

## 9. Completion rule

A feature is not complete if its implementation silently resolves a Deferred or Experimental question by coupling persisted authoritative data to one answer.

The feature must either:

- remain behind an extension/version boundary;
- remain a reversible projection;
- or explicitly promote the decision through the recorded process.
