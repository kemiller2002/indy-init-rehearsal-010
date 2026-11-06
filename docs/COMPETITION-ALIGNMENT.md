# Indy Init Competition Alignment

Status: current competition alignment profile.

Source reviewed: Indy Init 2026 published hackathon and conference pages.

## 1. Published challenge

The published challenge is to:

- start from a blank repo
- build a production-grade app
- build the software factory that ships it
- use AI
- demo the machinery as well as the product
- defend the technical decisions live

The Software Factory prize explicitly emphasizes:

- harness
- standards
- tests
- security
- pipeline
- reusability on another problem

The event's technical themes also explicitly emphasize:

- agents and orchestration
- context/retrieval/compaction/token budgets
- property tests
- spec-driven generation
- harnesses/conventions
- testing/CI/CD/evals
- security
- observability

This project should make each of those visible.

## 2. Software Factory alignment

### Harness

Visible artifacts:

- Conditor bootstrap
- Ordo/SDE domain state
- ROS work execution
- semantic compiler
- command/event registry
- domain pack contract
- AI proposal schemas
- tool gateway

Demo proof:

- blank repo -> bootstrap -> working typed structure
- semantic registry -> generated tests/contracts
- same factory reusable for EDF later

### Standards

Visible artifacts:

- PRODUCT-REQUIREMENTS.md
- INVESTIGATION-SEMANTICS.md
- DESIGN-DECISION-BOUNDARIES.md
- SCREEN-CONTRACTS.md
- SECURITY-THREAT-MODEL.md
- domain-pack contract

Demo proof:

- requirement -> contract -> implementation -> test

### Tests

Visible artifacts:

- 250+ acceptance scenarios baseline as planning evolves
- domain test matrix
- state-space explorer
- property tests
- mutation testing
- accessibility matrix
- offline/concurrency chaos
- AI contract fuzzing

Demo proof:

- show semantic mutation being caught
- show illegal transition blocked
- show replay equivalence

### Security

Visible artifacts:

- explicit trust boundaries
- scoped tool capabilities
- prompt-injection corpus
- credential isolation
- untrusted evidence boundary
- classification-based AI disclosure

Demo proof:

- malicious evidence cannot become instructions
- model cannot self-escalate tool capability
- secrets absent from model/investigation/proof output

### Pipeline

Visible artifacts:

- GitHub workflow
- requirements traceability
- semantic contract check
- test gates
- Praxis metrics
- Aegis faults
- deployment
- proof bundle

Demo proof:

- commit -> CI -> semantic validation -> deploy -> proof bundle

### Reusability

Visible artifacts:

- generic investigator kernel
- provider abstraction
- domain packs
- EDF follow-on
- semantic versioning
- Conditor

Demo proof:

- show generic investigation
- show how EDF becomes a pack without kernel fork
- show one experimental EDF semantic registered as extension

## 3. Building with AI alignment

### Agents/orchestration

Capabilities:

- specialized adversarial roles
- blind phase
- reconciliation
- bounded autonomous investigation
- governed tools
- proposal dependency tracking

Demo:

- Generator, Skeptic, Evidence Auditor disagree
- Experiment Designer proposes discriminating action
- human accepts action through policy

### Context/prompting

Capabilities:

- bounded semantic context
- local WASM prefilter
- classification-aware disclosure
- context manifest
- prompt version registry
- proof-carrying proposals

Demo:

- inspect exactly what evidence the model saw
- show excluded unrelated/sensitive evidence
- compare prompt versions on same frozen state

### Scaling generation

Capabilities:

- semantic compiler
- generated schemas/tests
- state-space exploration
- mutation tests
- screen contracts
- proof graph

Demo:

- adding a command creates missing-handler/test/codec failures until complete

## 4. Production-grade claim alignment

### Secure
SECURITY-THREAT-MODEL + tests.

### Tested
Unit/property/state-space/mutation/UI/accessibility/security/offline tests.

### Observable
Aegis + Praxis + ROS + Factory View + tool invocation provenance.

### Resilient
Offline local-first, semantic conflict handling, retry idempotency, AI/provider degradation.

### Scalable
Bounded context, local prefilter, incremental event/snapshot architecture, provider abstraction.

### Affordable
AI cost tracking, caching, local deterministic analytics, planner budgets, model-role selection.

## 5. Floor-judging visible checklist

A judge should be able to see within minutes:

1. Working product.
2. Working machinery.
3. One real invalid action blocked.
4. One real AI proposal with evidence/falsifier.
5. One test/property proving a core invariant.
6. One security control working.
7. One live/real metric.
8. One requirement traced to code/test.
9. One offline/resilience behavior.
10. One example of reuse beyond this app.

## 6. Strongest prize posture

The project should deliberately optimize for the Software Factory prize by showing:

- repeatable setup
- generated/validated semantic contracts
- explicit agent context
- state-directed development
- testing beyond examples
- security by construction
- observability/provenance
- reusable domain pack architecture
- build proof bundle

The product itself should still be strong enough to compete for Wildcard because the investigation experience is unusual and visually demonstrable.

## 7. Community impact posture

The generic investigator can support:

- software incidents
- nonprofit operational issues
- small-business process failures
- accessibility/service failures
- community project debugging
- research experiments

Do not invent a community-impact claim. Demonstrate that the kernel is accessible, generic, and reusable by people outside a single engineering team.

## 8. Evidence over claims

Any statement made to judges should have a visible evidence path.

If we say:

"AI cannot bypass policy"

show the test/blocked command.

If we say:

"reusable next month"

show Conditor + generic kernel + EDF pack boundary.

If we say:

"offline"

disconnect and use it.

If we say:

"secure"

show capability denial/prompt-injection test/secrets boundary.

If we say:

"observable"

show a fault or tool invocation trace.

If we say:

"spec-driven"

show requirement -> generated contract/test -> code.

## 9. Do not over-index on product polish alone

A polished investigation UI without machinery undershoots the challenge.

A sophisticated factory without a compelling product undershoots the demo.

The intended balance:

```text
Product creates the reason to care.
Factory creates the reason to trust it.
Proof bundle creates the reason to believe us.
```
