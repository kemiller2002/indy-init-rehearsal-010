# Competition Ambition Profile

Status: ambitious competition profile layered on top of the durable Root-Cause Investigator.

The competition build should demonstrate more than a capable root-cause application. It should demonstrate a new way of building and using AI software: explicit semantics, adversarial reasoning, inspectable causality, reversible research, offline-first execution, and provable engineering provenance.

This profile is intentionally aggressive.

The rule is:

> Push capability at the projection, orchestration, and experiment layers. Do not weaken authoritative semantics to create a flashy demo.

## 1. Competition thesis

The application should make three claims visible:

1. AI can reason more usefully when its work is constrained by explicit domain state and evidence.
2. Users can inspect why the system believes something, what would change that belief, and what remains unknown.
3. The same engineering framework that governs the investigation also governs the software's creation.

The demo should therefore show both:

```text
Investigation under governance
```

and:

```text
Software creation under governance
```

## 2. Capability bands

### Band A: durable core

Must remain useful after competition:

- explicit investigation semantics
- typed commands/events
- legal capabilities/blockers
- structured AI proposals
- GitHub persistence
- offline/local durability
- causal confirmation
- corrective-action verification
- reports
- domain packs

### Band B: ambitious reusable capabilities

Strong candidates for competition because they are also long-term product differentiators:

- time travel
- semantic diff
- "what changed my mind?"
- multi-agent adversarial investigation
- counterfactual branches
- evidence dependency graph
- causal-set visualization
- sensitivity analysis
- policy/model/domain-version comparisons
- research tournament
- investigation replay
- proof bundle

### Band C: competition-only projection/polish

May be optimized specifically for judges but must remain a projection:

- spectator mode
- animated investigation replay
- factory telemetry board
- demo choreography
- judge summary
- build provenance timeline
- requirements-to-evidence live trace

## 3. Time travel

The product should be able to reconstruct an investigation at any authoritative aggregate version.

User capabilities:

- scrub through investigation history
- inspect state at version N
- compare version N with current
- see which hypotheses were active then
- see what was unknown then
- see which actions were legal then
- see policy/domain versions active then

Time travel is read-only unless explicitly used to fork a new branch.

### Judge moment

Show a hypothesis looking strong at version 18, then scrub to version 24 after contradictory evidence arrives and watch:

- its support posture change
- a contradiction appear
- ConfirmCause become blocked
- next recommended action change

This visually proves that the AI is not merely telling a story.

## 4. Semantic diff

A diff between two investigation versions should describe meaning changes, not JSON changes.

Example:

```text
v18 -> v24

+ Evidence E12 admitted
+ Contradiction C4 detected
H1: Supported -> Weakened
H2: Plausible -> Supported
ConfirmCause: Available -> Blocked
New obligation: Resolve C4
Recommended action: Test B -> Test D
```

The diff is a derived projection from event/state changes.

## 5. "What changed my mind?"

For any major posture change, provide an explanation.

Examples:

- Why did H1 stop leading?
- Why is confirmation blocked now?
- Why did the recommended next test change?
- Why was the investigation reopened?
- Why did conclusion posture change?

The answer should cite:

- triggering events
- affected evidence
- policy rules
- newly created/cleared blockers
- relevant negative knowledge

This should be deterministic where possible and AI-narrated only as an explanation layer.

## 6. Counterfactual branches

Allow a user to create a read-only or sandbox branch from an immutable investigation version.

Examples:

- What if evidence E12 is invalid?
- What if assumption A3 is false?
- What if deployment D had not occurred?
- What if policy P2 were used instead of P1?
- What if H2 were the true cause?

A branch:

- has its own BranchId
- points to source InvestigationId/AggregateVersion
- cannot mutate authoritative source
- records hypothetical changes
- computes derived consequences
- may be promoted to a separate real investigation only explicitly

## 7. Sensitivity analysis

The system should be able to ask:

> Which pieces of evidence or assumptions are carrying the conclusion?

Possible derived analysis:

- remove one evidence relationship at a time
- invalidate one assumption
- change one unknown from accepted residual back to blocking
- compare resulting capabilities/conclusion posture

Outputs:

- robust conclusion
- fragile conclusion
- pivotal evidence
- pivotal assumption
- pivotal policy rule

Do not convert sensitivity into an unexplained numeric confidence score.

## 8. Evidence dependency graph

Visualize:

- evidence sources
- derived evidence
- common-source dependencies
- supporting/contradicting relationships
- supersession
- artifacts

The graph should make false corroboration visually obvious.

Example:

```text
Log file L1
 ├── Chart C1
 ├── Table T1
 └── AI summary S1
```

These are three records but one underlying source lineage.

## 9. Causal-set visualization

Visualize proposed causal structure without requiring a universal DAG ontology.

Show:

- outcome
- causal members
- root/contributing/trigger/enabling roles
- mechanism
- evidence
- unknowns
- contradictions
- verification targets

Support causal sets where multiple members jointly matter.

## 10. Uncertainty map

Provide a derived view of unresolved investigation uncertainty.

Dimensions may include:

- blocking unknowns
- material unknowns
- unverified assumptions
- unresolved contradictions
- unsupported causal links
- missing verification
- source-integrity concerns

The visualization must remain explainable and inspectable.

## 11. Hypothesis battlefield

Create a high-information comparison surface for competing hypotheses.

Columns/regions:

- hypothesis
- explanation scope
- strongest support
- strongest contradiction
- assumptions
- predictions
- falsifying test
- negative knowledge
- pivotal unknown
- current derived posture

This is a judge-friendly representation of structured adversarial reasoning.

## 12. Multi-agent adversarial panel

Run specialized AI roles independently over the same frozen investigation state.

Suggested roles:

- Hypothesis Generator
- Skeptic / Red Team
- Evidence Auditor
- Experiment Designer
- Causal Analyst
- Verification Auditor

Each produces structured proposals.

The system then shows:

- agreements
- disagreements
- unique discoveries
- duplicated reasoning
- source dependencies
- cost/latency
- accepted/rejected proposals

No model wins by vote.

Authoritative acceptance still passes through policy.

## 13. Blind first-pass mode

For research-quality multi-agent comparison:

- agents receive the same frozen context
- agents do not see each other's output initially
- outputs are recorded independently
- reconciliation happens afterward

This avoids artificial consensus and enables meaningful comparison.

## 14. Reconciliation round

After blind outputs:

- expose disagreements
- ask agents to critique specific competing proposals
- identify evidence that would resolve disagreement
- generate one or more discriminating actions

The result remains proposals.

## 15. Model/provider tournament

Support comparing OpenAI, Anthropic, and future providers on identical tasks.

Track:

- hypothesis diversity
- evidence citation precision
- contradiction detection
- falsifying-test quality
- proposal acceptance/edit/reject rate
- cost
- latency
- downstream investigation value

This can become a long-term research harness, not just a demo.

## 16. Policy tournament

Apply multiple investigation policies to one immutable snapshot.

Example:

```text
Same evidence snapshot
  -> Generic policy
  -> EDF v0.3
  -> EDF experimental vNext
```

Compare:

- blockers
- confirmation eligibility
- unknown treatment
- obligations
- next actions
- conclusion posture

This directly supports ongoing EDF research.

## 17. Prompt tournament

For one agent role, run multiple prompt/instruction versions over the same frozen context.

Track behavioral differences without mutating source state.

This lets the platform become a prompt/system-design experiment harness.

## 18. "What would falsify this?"

Every active hypothesis should expose one-click generation of:

- necessary prediction
- strongest plausible falsifier
- cheapest useful falsifier
- safest falsifier
- best discriminating test against top alternative

This should be one of the signature interactions.

## 19. "What are we missing?"

One action should explicitly search for:

- unconsidered causal mechanisms
- hidden assumptions
- unexplained observations
- evidence without relationships
- contradictions not represented explicitly
- common-source evidence dependencies
- untested alternatives
- scope gaps
- measurement/instrument failure

This is not generic brainstorming. It is a structured completeness challenge.

## 20. Coverage map

Derived investigation coverage should show which material observations/claims are:

- explained
- partially explained
- unexplained
- contradicted
- out of scope

Keep this generic. EDF-specific ContextCoverage remains an extension.

## 21. Evidence orphan detector

Detect:

- evidence linked to nothing
- conclusions relying on no evidence
- hypotheses with no predictions
- verification with no criterion
- corrective action with no target cause
- unknowns that block nothing and are never assessed

These become actionable quality diagnostics, not necessarily authoritative faults.

## 22. Circular reasoning detector

Detect likely cycles such as:

```text
H1 is supported because interpretation I1
I1 exists because H1 explains E1
```

or derived evidence chains where the same inference is used as its own support.

This is a derived analytical warning and an ideal AI + graph-analysis collaboration feature.

## 23. Contradiction miner

AI may scan current authoritative state for candidate contradictions that have not yet been modeled.

Output:

- potential contradiction
- participating entities
- why they appear incompatible
- possible scope reconciliation

Human accepts/rejects the contradiction proposal.

## 24. Assumption miner

AI may identify implicit premises in:

- hypotheses
- interpretations
- experiments
- causal claims
- corrective actions

Accepted assumptions become explicit state.

This makes hidden reasoning inspectable.

## 25. Prediction audit

Before an experiment/action runs, the system should ask:

- were predictions recorded beforehand?
- are they different across hypotheses?
- is the test actually discriminating?
- could the result be explained either way?

This helps prevent post-hoc storytelling.

## 26. Experiment pre-registration

For high-value actions:

- freeze predictions
- record procedure
- record expected alternatives
- record success/falsification criteria
- hash/commit pre-registration state

Later result interpretation can show whether predictions changed after the fact.

This is powerful for EDF research.

## 27. Experiment reproducibility bundle

An experiment/action can generate a bundle containing:

- source investigation version
- predictions
- inputs
- environment
- procedure
- artifacts
- result
- interpretation
- policy/domain versions

A second investigator/model can reproduce it.

## 28. Investigation replay

Provide an animated/readable replay driven by authoritative events.

Replay may show:

1. observation arrives
2. hypotheses proposed
3. predictions recorded
4. evidence added
5. H1 strengthens
6. contradiction appears
7. H1 falsified
8. H2 becomes leading
9. cause confirmed
10. correction implemented
11. verification succeeds

Replay is projection only.

## 29. Spectator mode

A read-only presentation mode optimized for a judge/audience.

Displays:

- problem
- active hypotheses
- current evidence
- current blocker
- next action
- causal map
- live event stream
- persistence state

It must display real state, not scripted fake state.

## 30. Factory telemetry view

Show the software factory building the product.

Possible live signals:

- ROS current work item
- requirements implemented
- tests passing/failing
- Praxis metrics
- files/hunks changed
- Aegis faults
- commits
- deployment status
- framework versions
- traceability completeness

This connects the product demo to the Echelon engineering thesis.

## 31. Requirements-to-code trace

For a selected product behavior, show:

```text
Requirement
  -> acceptance scenario
  -> ROS work item
  -> source files
  -> tests
  -> commit
  -> metrics
  -> runtime evidence
```

This may be one of the strongest competition differentiators because it demonstrates defensibility of AI-generated software.

## 32. Build provenance proof bundle

Generate a Folio report proving the competition build's provenance.

Include:

- blank/start state evidence
- kickoff timestamp
- framework versions
- requirements baseline
- commit timeline
- work items
- tests
- metrics
- build/deployment evidence
- known shortcuts/deviations
- final artifact checksum/version

This is useful regardless of judging.

## 33. Self-diagnostic mode

Use the investigator on its own runtime/build evidence.

Examples:

- deployment fails
- a test regresses
- a UI requirement fails
- performance changes

Create a real investigation from the product's own Aegis/Praxis/ROS evidence.

This is a highly compelling recursive demonstration.

## 34. Live provenance drill-down

From any UI claim or state, allow:

```text
Why?
  -> event
  -> command
  -> actor
  -> evidence
  -> policy
  -> source artifact
```

From any AI proposal:

```text
Why?
  -> model/provider
  -> prompt version
  -> frozen context version
  -> evidence refs
  -> assumptions
```

## 35. Semantic search

Natural-language search should compile to structured investigation retrieval where possible.

Examples:

- "show me all falsified explanations"
- "what evidence depends on this log?"
- "which assumptions still block confirmation?"
- "what changed after experiment 7?"
- "show evidence that contradicts the current leader"

AI may interpret query intent, but results come from authoritative state.

## 36. Investigation command palette

On iPad/keyboard:

- Ask investigator
- Add evidence
- Add observation
- Add hypothesis
- Challenge current leader
- What next?
- What changed?
- Time travel
- Compare hypotheses
- Generate report
- Open replay
- Open factory view

This makes advanced features discoverable without cluttering every screen.

## 37. Evidence camera/import

On supported devices, allow fast evidence capture:

- photo
- screenshot
- pasted text
- file
- URL
- manual measurement

Capture preserves provenance and never auto-promotes AI extraction to fact.

This is a long-term useful mobile capability.

## 38. Snapshot share

Create a read-only snapshot link/package at an exact Investigation/AggregateVersion.

The snapshot must not silently update as the source investigation changes.

Useful for:

- judge review
- external expert review
- reproduction
- report evidence

Public sharing itself remains deployment/security controlled.

## 39. Expert-review packet

Generate a compact packet asking an external expert to assess:

- selected hypothesis
- selected evidence
- contradictions
- unknowns
- exact questions

Returned expert input enters as reported/testimony evidence with provenance, not truth by authority.

## 40. Automated demo health check

Before presentation, run a deterministic checklist:

- deployment reachable
- local/offline mode works
- GitHub persistence authorized
- AI provider available
- fallback provider/model configured if allowed
- demo investigation resettable
- spectator mode works
- report generation works
- no secret visible
- required acceptance smoke tests pass

Output a green/red readiness view.

## 41. Graceful degradation

Competition ambition must not create a single brittle dependency.

If one capability fails:

- AI provider down -> manual mode + second provider if configured
- GitHub unavailable -> local pending state
- fancy graph fails -> semantic list view
- domain pack unavailable -> generic fallback
- animation disabled -> static accessible view

The demo should still tell a coherent story.

## 42. Ambition prioritization

If time permits, prioritize the competition differentiators in this order:

1. investigation time travel + semantic diff
2. "what changed my mind?"
3. challenge current leader
4. hypothesis battlefield
5. multi-agent blind panel
6. counterfactual branch
7. evidence dependency graph
8. factory telemetry/proof bundle
9. investigation replay/spectator mode
10. policy/model tournament

These build on the durable kernel rather than replacing it.

## 43. Competition success criterion

The audience should leave understanding that this is not:

- another chatbot
- a prettier incident form
- an AI-generated 5 Whys
- a scripted demo

They should see a system that can:

- disagree with itself visibly
- falsify its own leading explanation
- explain why its state changed
- show which evidence is carrying a conclusion
- simulate alternate reasoning without corrupting reality
- prove how both the investigation and the application were built
