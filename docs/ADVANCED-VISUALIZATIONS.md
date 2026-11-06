# Advanced Investigation Visualizations

Status: semantic visualization contract.

Visualizations are projections of authoritative/derived state. They must never become a second source of truth.

Every visualization requires an accessible non-graph alternative.

## 1. Hypothesis battlefield

Purpose:
Compare competing explanations at a glance.

Required semantic rows/columns:

- hypothesis statement
- current lifecycle state
- derived comparison posture
- strongest support
- strongest contradiction
- assumptions
- blocking unknowns
- predictions
- falsifying action
- evidence dependency warning

Interactions:

- select hypothesis
- challenge
- compare
- add evidence
- propose test
- inspect why posture changed

## 2. Evidence dependency graph

Nodes:

- source artifacts
- evidence
- findings
- derived calculations
- interpretations where useful

Edges:

- DerivedFrom
- DependsOn
- Supersedes
- Corroborates where meaningful

Visual requirement:
Common-source clusters must be visually recognizable.

Accessible alternative:
hierarchical lineage table.

## 3. Investigation reasoning graph

Optional graph projection connecting:

- observations
- hypotheses
- evidence
- unknowns
- assumptions
- contradictions
- predictions
- actions
- findings
- causal assertions
- corrective actions
- verification

Do not force all entities on screen simultaneously.

Use filtering/lenses.

## 4. Causal-set view

Purpose:
Explain proposed/confirmed causal structure.

Must display:

- outcome
- causal members
- member role
- mechanism
- evidence
- unresolved contradiction
- unknowns
- corrective target
- verification target

Support one-to-many and many-to-one relations.

## 5. Uncertainty heatmap

Rows may represent:

- hypotheses
- causal assertions
- corrective actions

Columns may represent:

- evidence completeness
- contradiction burden
- unknown burden
- assumption burden
- verification burden
- source integrity concerns

Cells are derived categorical states with drill-down.

Avoid unexplained gradients that imply numeric probability.

## 6. Version timeline

Show semantic events over aggregate version.

Markers:

- hypothesis proposed/falsified/reopened
- contradiction
- evidence admission
- cause confirmation/reopen
- correction
- verification
- resolution/reopen
- policy/domain version changes

Supports time travel.

## 7. Semantic diff view

Compare version A/B:

- entity additions
- state transitions
- blocker changes
- capability changes
- derived posture changes
- policy/domain changes

Avoid raw JSON by default.

## 8. Agent panel

Show per-agent:

- role
- provider/model
- unique proposals
- agreements/disagreements
- evidence cited
- cost
- latency
- accepted/rejected

Accessible alternative:
structured comparison table.

## 9. Research tournament view

Axes:

- model/provider
- prompt version
- policy version
- domain version
- role

Metrics:

- useful proposals
- contradiction detection
- evidence precision
- cost/latency
- human disposition
- downstream effect

Do not declare an overall winner unless a specific experimental evaluation rule defines one.

## 10. Factory telemetry

Show:

- current ROS item
- build state
- test count/pass/fail
- Praxis metrics
- Aegis faults
- requirements coverage
- commit activity
- deployment state

This is engineering telemetry, not investigation truth.

## 11. Requirements proof graph

Nodes:

- requirement
- acceptance scenario
- work item
- source file
- test
- commit
- runtime evidence

Purpose:
Demonstrate traceability from intent to deployed behavior.

## 12. Replay mode

Drive animations from event sequence.

Rules:

- deterministic replay
- pause/step
- current aggregate version shown
- no fake event ordering
- reduced-motion alternative
- screen-reader summary of each step

## 13. Visual regression evidence

Key visualizations should have:

- iPad landscape captures
- iPad portrait captures
- narrow phone captures
- desktop captures
- high zoom
- reduced motion

Percepta should eventually generate/validate application-specific visualization contracts.
