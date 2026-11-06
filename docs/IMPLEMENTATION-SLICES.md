# Implementation Slices

Status: planning only before hackathon kickoff.

This document defines an implementation order that produces a durable product incrementally while preserving hackathon integrity. It does not contain the competition application's implementation.

The slices are ordered to prove semantic integrity before adding convenience.

## Guiding rule

A slice is complete only when it leaves the system in a usable and internally coherent state.

Do not build a broad but semantically hollow demo.

Prefer:

```text
small + complete + evidenced
```

over:

```text
many screens + weak semantics
```

## Pre-kickoff preparation

Allowed planning work includes:

- requirements
- semantic model
- policy contract
- persistence contract
- UI contracts
- acceptance scenarios
- security threat model
- test strategy
- framework integration
- deployment strategy
- agent instructions
- demo scenarios

Do not prebuild the competition application implementation.

## Slice 0: Bootstrap and evidence baseline

Goal: prove the build starts from the required blank/approved state and establish engineering provenance.

At kickoff:

- record initial repository state
- record timestamp
- record framework/component versions
- initialize Conditor/bootstrap path
- initialize ROS/Ordo/Aegis/Praxis/Limen/Forma/Folio as permitted
- capture build environment
- create initial implementation commit
- load approved requirement baseline
- establish requirements-to-test traceability

Completion evidence:

- repository provenance
- clean build
- framework inventory
- ROS work state
- Praxis baseline
- no unexplained prebuilt application code

## Slice 1: Generic semantic kernel

Goal: create the smallest useful typed investigation engine with no EDF dependency.

Implement:

- Investigation identity/lifecycle
- Observation
- Claim
- Interpretation
- Hypothesis
- Evidence
- EvidenceRelationship
- Unknown
- Assumption
- Contradiction
- Obligation
- Prediction
- InvestigationAction
- Finding
- NegativeKnowledge
- causal assertion
- corrective action
- verification
- conclusion
- provenance
- scope
- semantic/version manifest
- extension envelope

Implement only the CoreStable semantics needed for the first use case.

Do not implement:

- EDF metrics
- EDF topology
- universal causal graph
- universal proposition AST
- general rules DSL

Implementation contracts:

- COMMAND-EVENT-CATALOG.md
- STATE-TRANSITION-TABLES.md
- FSHARP-TYPE-SHAPES.md
- DOMAIN-TEST-MATRIX.md

Completion evidence:

- core semantic tests
- table-driven state/command tests
- pure evolve determinism tests
- extension round-trip tests
- no EDF vocabulary in generic core
- no infrastructure dependencies in the Domain project

## Slice 2: Policy and capability engine

Goal: make legal behavior deterministic before UI starts inventing it.

Implement:

- command envelope
- transition decision
- blocker model
- obligations
- policy version identity
- core invariant firewall
- capability projection
- cause-confirmation gate
- resolution gate
- review placeholder/contracts
- optimistic aggregate version checking

Completion evidence:

- blocked/allowed scenarios
- structural transition and policy-layer composition tests
- all protected transitions explain blockers
- UI-independent capability tests
- proposal acceptance revalidation tests
- policy attribution in emitted events

## Slice 2A: Semantic compiler and exhaustive transition validation

Goal: turn the semantic model into build-time leverage.

Implement:

- typed semantic registry
- command/event/capability metadata
- structural transition generation
- handler/evolve/codec completeness checks
- generated state diagrams
- semantic contract manifest/diff
- state-space exploration
- property tests
- initial mutation testing

Completion evidence:

- zero registered commands without handlers
- zero emitted events without evolve/codec support
- lifecycle state-command matrix explored
- critical semantic mutations killed
- semantic contract manifest generated

## Slice 3: Local investigation workspace

Goal: allow a complete investigation locally before remote persistence or AI is required.

Implement:

- Investigation Start
- Investigation Workspace
- observation capture
- manual hypothesis capture
- evidence capture
- relationships
- unknowns/assumptions
- contradictions
- next legal actions
- cause review
- corrective action/verification
- history
- local persistence state

Use Forma/Limen.

Completion evidence:

- iPad portrait/landscape
- narrow phone
- keyboard/accessibility
- UI projection tests
- workspace understandable without chat

## Slice 4: Durable local-first persistence

Goal: work safely through refresh/offline conditions.

Implement:

- local durable store
- snapshot
- event/command history needed for recovery
- queued command states
- restart recovery
- local/remote-truth indicator abstraction
- corruption handling

Completion evidence:

- refresh recovery
- browser restart recovery
- queue durability
- no false synced state

## Slice 5: GitHub persistence adapter

Goal: add auditable remote persistence without coupling domain to GitHub.

Implement:

- selected repository binding
- GitHub load
- optimistic concurrency
- atomic semantic write strategy
- remote version token
- conflict detection
- canonical file layout adapter
- import/export subset
- artifact URL references

Completion evidence:

- sync
- offline/reconnect
- stale conflict
- independent additions
- semantic conflict
- no silent overwrite
- provider-independent InvestigationId

## Slice 6: AI proposal loop

Goal: make natural-language interaction useful without giving AI authority.

Implement:

- AI adapter abstraction
- context builder
- minimal state projection
- prompt/version identity
- structured proposal types
- proposal UI
- accept/edit/reject
- evidence references
- unknown/question proposals
- competing hypothesis generation
- next-action proposal
- challenge mode
- stale-proposal detection

Completion evidence:

- observation/inference separation
- no fabricated Evidence
- prompt-injection test
- negative-knowledge retrieval
- challenge-leading-hypothesis test
- AI outage fallback

## Slice 6A: Tool execution and local analytics

Goal: let the investigator perform real bounded work.

Implement:

- tool registry
- read/pure-analysis tool gateway
- scoped tool capabilities
- invocation provenance
- Finding pipeline
- GitHub read tool
- local WASM hashing/diff/graph analysis
- evidence ingestion for text/file/log/diff

Completion evidence:

- model cannot invoke undeclared tool/resource
- tool result lineage preserved
- deterministic local operation works offline
- raw source survives parse/extraction

## Slice 6B: Bounded autonomous investigation

Goal: close the investigation loop without losing governance.

Implement:

- Level 1 read-only autonomous session
- budget
- stop conditions
- planner integration
- tool invocation
- proposal/finding capture
- challenge pass
- human checkpoint

Optional:
narrow Level 2 auto-admission of explicitly configured deterministic low-risk findings.

Completion evidence:

- protected transitions stop for human
- budget enforced
- loop detection works
- crash/restart leaves authoritative state valid
- autonomous work produces inspectable provenance

## Slice 7: Investigation strategy

Goal: make the product materially better than a structured notebook.

Implement:

- predictions
- discriminating action comparison
- blocking-question prioritization
- contradiction-resolution suggestions
- assumption verification suggestions
- qualitative cost/risk/information-value explanation
- alternative-explanation pressure

Do not require a numeric scoring formula.

Completion evidence:

- chooses discriminating action over merely confirmatory action in controlled scenarios
- explains recommendation
- respects cost/risk override

## Slice 7A: Proof-carrying proposals and planner

Goal: make AI output inspectable enough to challenge and revalidate.

Implement:

- proposal evidence refs
- assumptions/scope
- falsifier
- expected state impact
- dependency-aware staleness
- proposal diff
- constrained next-action planner
- Pareto alternatives
- budget/risk constraints
- mitigation-versus-learning parallel plan

Completion evidence:

- stale only on material dependency change
- illegal action never recommended as executable
- recommendation tradeoffs explainable
- proposal acceptance still revalidates

## Slice 7B: Cross-investigation intelligence

Goal: reuse prior learning without contaminating current evidence.

Implement:

- similar-case retrieval
- opposite-cause retrieval
- reusable action templates
- scoped negative-knowledge suggestions
- recurrence projections
- permission/classification boundaries

Completion evidence:

- prior cause enters only as hypothesis suggestion
- unauthorized cases never enter AI/retrieval context
- similarity shows counterexamples when available

## Slice 8: Conclusion and corrective loop

Goal: demonstrate that investigation does not stop at a plausible cause.

Implement:

- cause confirmation review
- causal set support
- residual uncertainty
- mitigation/workaround/correction distinction
- verification criteria
- verification findings
- failed verification/reopen
- resolution gate

Completion evidence:

- cannot resolve before required verification
- failed correction returns to work
- causal set supported
- accepted residual uncertainty remains visible

## Slice 9A: Time travel and semantic diff

Goal: make the event model visibly useful.

Implement:

- reconstruct at aggregate version
- version scrubber
- semantic diff
- "what changed my mind?"
- why capability/blocker changed
- replay step mode

Completion evidence:

- deterministic version reconstruction
- posture changes explained from real events
- reduced-motion accessible replay

## Slice 9B: Counterfactual and sensitivity

Goal: support aggressive reasoning without corrupting authoritative state.

Implement:

- immutable branch source
- hypothetical evidence/assumption changes
- branch semantic diff
- evidence/assumption sensitivity
- "what would change the answer?"
- explicit sandbox labeling

Completion evidence:

- source investigation unchanged
- branch never produces authoritative events in source
- promotion creates explicit new investigation/follow-up

## Slice 9C: Advanced reasoning views

Goal: make epistemic structure immediately visible.

Implement:

- Hypothesis Battlefield
- evidence dependency graph/list
- causal-set view
- uncertainty map
- version timeline
- semantic diff view

Completion evidence:

- accessible non-graph alternatives
- common-source dependency visible
- causal set does not require universal DAG

## Slice 9D: Adversarial multi-agent panel

Goal: demonstrate independent AI reasoning roles.

Implement:

- blind frozen-state runs
- Hypothesis Generator
- Skeptic
- Evidence Auditor
- Experiment Designer
- Causal Analyst
- Verification Auditor
- structured proposal normalization
- disagreement/reconciliation view
- cost/latency/provenance

Completion evidence:

- agents do not see each other in blind phase
- derived agents not counted independent
- acceptance still passes through normal commands/policy
- one agent failure does not corrupt panel

## Slice 9E: Investigation quality diagnostics

Goal: expose reasoning defects automatically.

Implement:

- orphan evidence detection
- hidden assumption proposals
- contradiction mining
- circular reasoning warning
- prediction audit
- falsifier generation
- missing-explanation coverage map

Completion evidence:

- diagnostics are reviewable derived warnings/proposals
- no diagnostic silently mutates authority

## Slice 9: Reporting

Goal: create professional portable output.

Implement:

- interim report
- final report
- source InvestigationId/version
- hypotheses considered
- evidence
- falsified explanations
- unknowns
- contradictions
- causal reasoning
- corrective action
- verification
- residual uncertainty
- Folio projection

Completion evidence:

- report snapshot identity
- unresolved work visible in interim report
- accessible print structure where supported

## Slice 10: Security hardening required for public demo

Goal: ensure demo shortcuts do not expose credentials or unsafe rendering.

Implement/verify:

- auth approach
- secrets exclusion
- sanitization
- CSP where applicable
- prompt-injection boundaries
- least-privilege repo access
- safe external URL handling
- user-safe Aegis faults

Completion evidence:

- no secrets in repo/export/report
- malicious content inert
- untrusted evidence cannot alter AI/system instructions

## Slice 11A: Factory proof and spectator mode

Goal: make the engineering thesis visible to judges.

Implement:

- spectator mode
- investigation replay
- factory telemetry
- requirements proof graph
- build provenance proof bundle
- demo health check
- optional self-diagnostic investigation

Completion evidence:

- telemetry sourced from real ROS/Praxis/Aegis/GitHub evidence
- proof graph links real artifacts
- spectator view is read-only
- proof bundle identifies exact build/version

## Slice 11B: Engineering proof bundle

Goal: prove what the build actually satisfies.

Implement:

- build identity
- kickoff/start provenance
- requirement/scenario/work/test/commit trace
- Praxis summary
- ROS summary
- Aegis summary
- semantic contract digest
- accessibility/security evidence
- known gaps/deviations
- Folio proof report

Completion evidence:

- proof bound to exact commit/build/deployment
- missing evidence remains visible
- factory view links into real proof data

## Slice 11: Hackathon polish and factory reveal

Goal: demonstrate both the app and the method used to create it.

Implement:

- demo scenario
- visible state change
- visible blocker
- visible falsification
- visible offline/persistence truth if useful
- factory metrics
- requirements coverage
- tests
- Praxis metrics
- ROS/Ordo evidence
- Aegis diagnostics
- deployment

Do not sacrifice core correctness for decorative scope.

## Slice 12: Post-hackathon EDF domain pack

Goal: make EDF the first real specialization without changing generic core.

Implement initially:

- EDF manifest
- versioned vocabulary
- EDF experiment import/mapping
- metric definition extensions
- unknown-effect extension
- evidence-dependency projection
- EDF policy
- EDF report projection
- EDF AI guidance
- generic fallback

Keep active research constructs Experimental unless current evidence justifies DomainStable.

Completion evidence:

- generic core source unchanged for ordinary EDF extensions
- EDF pack can be disabled while core investigation remains readable
- old/new EDF policy comparison works

## Slice 13: EDF recursive research mode

Goal: use the investigator to test EDF itself.

Implement:

- frozen investigation snapshots
- domain/policy version comparison
- model-run comparison
- research run provenance
- pack semantic comparison
- reassessment without history rewrite

Example experiments:

- ContextCoverage v1 versus proposed v2
- EvidenceClosure policy variants
- UnknownEffect semantics
- control-point ranking variants
- calibration models

## Slice 13A: Research harness

Goal: turn the investigator into a methodology/model laboratory.

Implement:

- ResearchExperiment identity
- frozen cases
- treatment definitions
- model/prompt/policy/domain/planner variants
- blinded evaluation option
- outcome metrics
- reproducibility metadata
- regression comparison
- Folio experiment report

Completion evidence:

- treatments use identical frozen cases where required
- evaluator blinding works when enabled
- no arbitrary winner without defined metric
- EDF version comparisons remain non-destructive

## Slice 14: Long-term product growth

Candidates after validated need:

- collaboration/review
- hosted persistence
- database provider
- service API
- self-hosting
- portfolio search
- cross-investigation learning
- notifications
- organization policy
- richer importers
- enterprise access control
- multi-model workflows

Each should be added as a vertical slice with semantic acceptance evidence.

## Stop-the-line rules

Implementation stops for correction if:

- UI invents transition legality
- AI writes authoritative state outside commands
- GitHub concepts enter generic semantic types
- EDF research concepts enter generic core without promotion
- pending remote state is shown as synced
- unknown extension data is dropped
- migration fabricates historical information
- a blocker cannot explain its rule/state basis
- verification is collapsed into implementation
- original source text is overwritten by normalization
- accessibility-critical behavior fails
- a security shortcut starts becoming a core dependency

## Minimum viable durable product

If time is constrained, the minimum product worth keeping is:

- generic typed semantic kernel
- deterministic policy/capabilities
- one strong workspace
- local durability
- GitHub provider
- AI structured proposal loop
- cause/verification loop
- portable report

Everything else can grow around that.


## Pre-kickoff factory rehearsal

This work happens before the competition implementation begins and uses a sacrificial non-competition application.

Required sequence:

1. Pin the planning baseline.
2. Materialize a canonical execution contract through Conditor.
3. Validate Percepta contracts independently.
4. Bootstrap an empty repository.
5. Verify no competition application source is present.
6. Launch a fresh implementation agent with no oral/private briefing.
7. Require the agent to complete the live ROS mission.
8. Require machine-readable completion evidence.
9. Independently score build, tests, architecture boundaries, UI evidence, report evidence, and ROS state.
10. Classify generic failures back to the owning Echelon capability.
11. Repeat from an empty repository after fixes.

Competition-ready exit:

- no manual file copying;
- no missing canonical contract;
- no foundational architecture clarification required;
- all required machine-readable contracts discoverable;
- independent score passes;
- the second clean-room run demonstrates lower intervention/churn than the first.
