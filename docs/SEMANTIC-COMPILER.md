# Semantic Compiler and Generated Contracts

Status: ambitious engineering architecture profile.

The generic domain model already defines commands, events, states, semantic identities, policies, blockers, and capabilities. The competition build should exploit that structure to generate as much repetitive system glue and validation evidence as practical.

The goal:

> One semantic source should produce many synchronized artifacts.

## 1. Problem

Without generation, the following can drift independently:

- F# command/event unions
- transition tables
- AI structured-output schemas
- UI capability metadata
- Aegis fault codes
- documentation
- tests
- event serialization registries
- state diagrams
- report labels

Drift is especially dangerous in an agent-built application.

## 2. Semantic registry

Create a machine-readable/typed registry describing:

- command id
- event id
- source states
- target states
- required payload type
- entity target type
- protected transition flag
- blocker classes
- capability id
- display label
- accessibility label
- semantic version
- extension/core ownership

The registry can be built in typed F# rather than a separate DSL initially.

## 3. Generated transition tests

From structural transition metadata generate tests that prove:

- valid source states accepted structurally
- invalid source states rejected
- target state projection correct
- no undefined state transition accidentally exists

Policy-specific tests remain explicit.

## 4. Generated capability catalog

Generate a capability descriptor for UI/AI:

```text
CapabilityId
CommandId
DisplayName
Description
EntityTarget
HighConsequence?
RequiresConfirmation?
AccessibilityName
```

The domain still computes Available/Blocked/ReviewRequired.

## 5. Generated AI schemas

For commands/proposals whose payloads are safe for AI proposal:

- generate JSON schema
- generate semantic id/version
- generate allowed enum values
- generate validation adapter

AI never receives schemas for commands it should not propose.

## 6. Generated event serialization registry

Produce:

- event semantic id
- version
- encoder
- decoder
- compatibility metadata
- fallback behavior

Unknown extension events remain extension envelopes.

## 7. Generated Aegis catalog

Known invariant/transition faults can generate:

- stable code
- default user explanation
- developer explanation
- severity
- related capability
- remediation hint

Policy-generated blockers remain dynamic but can reference catalog entries.

## 8. Generated state diagrams

Generate Mermaid/DOT/other diagram source from the same lifecycle registry.

Diagrams become documentation evidence rather than hand-maintained artwork.

## 9. Generated transition matrix documentation

Generate tables similar to STATE-TRANSITION-TABLES.md from executable metadata after kickoff.

Pre-kickoff hand-written contract remains baseline; generated output is compared against it.

## 10. Generated test traceability

Each generated test should carry:

- command id
- transition id
- requirement ids
- acceptance scenario ids

Praxis/ROS can count semantic transition coverage.

## 11. Generated UI contract fragments

From capability/entity metadata generate the invariant part of a screen contract:

- action exists
- label
- unavailable behavior
- blocker presentation requirement
- entity type
- confirmation severity

Layout remains Percepta/UI-owned.

## 12. Generated analytics dimensions

Create stable telemetry dimensions:

- command type
- decision outcome
- blocker class
- event type
- capability
- domain pack
- policy version

No evidence text is needed for most engineering telemetry.

## 13. Generated report semantics

Folio projections may use registry metadata for:

- stable entity labels
- state names
- event descriptions
- domain-neutral glossary

Domain packs can override display vocabulary without changing semantic IDs.

## 14. Drift detector

CI should compare:

- registered command/event pairs
- implemented handlers
- implemented evolve cases
- transition tests
- serialization codecs
- capability descriptors

Fail if:

- command has no decision handler
- event has no evolve handler
- event can be emitted but not decoded
- core transition has no test
- protected capability lacks blocker UI metadata

## 15. Exhaustiveness as competition evidence

A dashboard can show:

```text
32 core commands
32 decision paths implemented
41 core events
41 evolve handlers
100% registered transitions tested
0 unhandled semantic IDs
0 missing codecs
```

These numbers must be generated from real code/registry.

## 16. Extension registry

Domain packs register:

- semantic IDs
- extension commands/events
- schemas
- policy rules
- projections
- compatibility

The compiler validates collisions:

- duplicate SemanticId
- incompatible version reuse
- undeclared core override
- missing fallback metadata

## 17. Schema generation

Generate canonical JSON Schema for portable DTOs where useful.

Benefits:

- AI output validation
- import validation
- artifact documentation
- cross-language interoperability later

Do not force pure F# domain records to equal wire DTO shape.

## 18. Contract snapshot

Each build should emit a semantic-contract manifest:

- core semantic version
- commands
- events
- capabilities
- extension IDs
- policy IDs
- domain packs
- schema digests

The build proof bundle can include its digest.

## 19. Contract diff

Between builds, generate semantic diff:

```text
+ Command: ReopenConfirmedCause
+ Event: ConfirmedCauseReopened
~ Policy: confirmation@2 -> @3
- Experimental semantic: edf:topology@0.2
```

Flag breaking changes.

## 20. Compatibility gate

CI blocks release when:

- persisted semantic meaning changed without version bump
- command/event removed without deprecation/migration policy
- schema changed incompatibly without new version
- domain pack requires unavailable semantic version

## 21. Agent benefit

Engineering agents can query the registry instead of inventing command names/state transitions.

This reduces hallucinated APIs and makes agent output more deterministic.

## 22. Competition demonstration

Show:

1. add/enable one transition
2. compiler detects missing handler/test/codec
3. implement them
4. dashboard turns complete
5. generated diagram updates

This demonstrates the engineering methodology in action.

## 23. Boundary

Do not build a giant metaprogramming framework if simple typed metadata plus generation accomplishes the goal.

The semantic compiler should reduce drift, not become a new source of complexity.
