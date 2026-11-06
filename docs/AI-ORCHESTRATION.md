# AI Investigation Orchestration Contract

Status: normative product and execution direction.

The AI layer assists investigation. It does not own investigation truth.

Its job is to help transform unstructured input into inspectable proposals, identify gaps and contradictions, propose discriminating next actions, and explain the current state without bypassing deterministic domain rules.

## 1. Operating principle

The AI should behave as an investigation collaborator, not an answer generator.

A useful loop is:

```text
Observe
  -> structure
  -> propose competing explanations
  -> identify discriminating unknowns
  -> propose next action
  -> receive finding
  -> update proposals
  -> challenge current leader
  -> repeat until policy permits stronger conclusion
```

The AI never turns narrative confidence into authoritative certainty.

## 2. Inputs

AI context should be assembled from explicit projections, not arbitrary concatenation of every stored artifact.

Context layers:

1. system safety and platform behavior
2. generic investigation semantics
3. active core policy summary
4. active domain-pack semantics
5. active domain policy
6. enabled experimental profile
7. scoped current investigation state
8. relevant historical state
9. authorized external/tool results
10. user request

Each layer should be identifiable for provenance/debugging.

## 3. Scoped context builder

The context builder should select only information relevant to the requested reasoning task.

Examples:

- proposing next action needs active hypotheses, predictions, blockers, unknowns, recent evidence, negative knowledge
- summarizing history needs event projection/history
- evaluating one hypothesis needs its evidence graph, assumptions, contradictions, and alternatives
- writing a report needs the authoritative report projection, not hidden internal prompt material

The context builder must not blindly send the full investigation if a smaller sufficient projection exists.

## 4. Context budget

The context builder should track:

- included entities
- omitted entities
- reason for inclusion
- reason for omission when material
- token/size budget
- semantic/version manifest

If important state is omitted due to budget, the model must not imply comprehensive review.

## 5. Minimal state view

For routine reasoning, a compact state view should include:

- current observation/problem statement
- active hypotheses and states
- material supporting/contradictory evidence
- blocking unknowns
- material assumptions
- unresolved contradictions
- negative knowledge relevant to current hypotheses
- open obligations
- legal capabilities
- persistence state
- active semantic/policy versions

This supports bounded reasoning while preserving the epistemic essentials.

## 6. Proposal types

AI output must use explicit proposal categories.

Examples:

- ProposedClaim
- ProposedInterpretation
- ProposedHypothesis
- ProposedEvidenceRelationship
- ProposedUnknown
- ProposedAssumption
- ProposedContradiction
- ProposedPrediction
- ProposedInvestigationAction
- ProposedCausalAssertion
- ProposedCorrectiveAction
- ProposedVerificationCriterion
- ProposedConclusionNarrative

The type determines what validation is required.

## 7. Structured output

Material proposals should be structured and schema-validated.

A proposal should include:

- proposal id
- type
- target entities
- statement/payload
- rationale
- source entity references
- assumptions
- expected impact
- semantic/domain version
- model/provider provenance
- uncertainty/limitations

Free-form chat text may explain a proposal but cannot substitute for the structured proposal when state mutation is possible.

## 8. Evidence citation

When an AI proposal depends on investigation evidence, it should reference EvidenceIds or other authoritative entity IDs.

It should not cite only prose fragments that cannot be traced back to state.

A proposal lacking sufficient support can still be shown, but should be labeled appropriately.

## 9. Source-type awareness

The AI must be told whether context items are:

- direct/report observation
- admitted evidence
- finding
- user comment
- agent interpretation
- assumption
- unknown
- negative knowledge
- historical conclusion
- external imported content

This reduces category collapse.

## 10. Prompt-injection boundary

Evidence and external artifacts are untrusted data.

The context builder must clearly delimit them from platform/domain instructions.

Instructions found inside evidence must be treated as evidence content unless the user explicitly promotes them through a trusted configuration path.

## 11. Tool use

AI tools may:

- fetch authorized external evidence
- run approved calculations
- query stored investigation state
- request domain-specific analysis
- invoke safe integrations

Tool output enters as provenance-bearing data.

Tool output does not automatically become authoritative evidence until admitted according to policy.

## 12. Read versus write tools

Separate AI tools into:

- Read/Analyze
- Propose
- AuthoritativeCommand

AuthoritativeCommand tools should be narrow and validated by the policy engine.

The model should never receive a generic "mutate investigation JSON" tool.

## 13. Human control

Consequential proposals should remain inspectable.

The user should be able to:

- accept
- reject
- edit
- defer
- ask why
- inspect supporting state
- compare alternatives

A domain/organization policy may auto-accept low-risk proposals, but that behavior must be explicit.

## 14. No fabricated evidence

The model must not create an Evidence record merely because a statement sounds plausible.

If the model infers something not present in evidence, it proposes:

- Interpretation
- Hypothesis
- Unknown
- InvestigationAction

as appropriate.

## 15. Questions

Questions should be generated to reduce important uncertainty.

Each proposed question should identify:

- what unknown/claim it targets
- which hypotheses it discriminates
- why the answer matters
- whether it blocks a transition
- expected information value qualitatively
- cost/burden when relevant

Avoid conversational questions that do not change investigation state.

## 16. Question prioritization

Prefer, in order when other factors are comparable:

1. questions that can falsify a leading hypothesis
2. questions that discriminate among multiple hypotheses
3. questions that resolve a blocking unknown
4. questions that resolve material contradiction
5. questions that verify a material assumption
6. questions that merely add more support to an already-supported explanation

Risk, cost, urgency, and safety may override this order.

## 17. Hypothesis generation

When warranted, the model should generate multiple hypotheses from meaningfully different causal mechanisms.

It should avoid superficial variants that merely rephrase one explanation.

It should also include an "instrumentation/data wrong" hypothesis when measurement failure is plausibly relevant.

## 18. Alternative explanation pressure

If one hypothesis becomes Leading, the AI should actively ask:

- what evidence would falsify it?
- what plausible alternative still explains the same evidence?
- what shared assumption could make both current reasoning paths wrong?
- could the measurement itself be wrong?
- is scope mismatch creating apparent support?

This is a required anti-premature-convergence behavior.

## 19. Negative knowledge retrieval

Before proposing a new hypothesis or action, the AI should receive relevant scoped negative knowledge.

If it proposes something previously ruled out, it should explain why new evidence justifies reopening it.

## 20. Evidence dependency awareness

Context should surface dependencies among evidence.

The AI must not describe multiple dependent records as independent confirmation.

## 21. Contradiction handling

When material contradiction exists, the AI should prefer explaining or resolving it over writing a smoother conclusion.

It may propose:

- source verification
- scope split
- measurement check
- temporal/version split
- semantic-definition check
- additional discriminating action

It must not choose one side silently.

## 22. Assumptions

The AI should state assumptions required by its reasoning.

A proposed action or conclusion that relies on material unverified assumptions should reference them.

## 23. Causal reasoning

The AI may reason about:

- temporality
- mechanism
- intervention
- counterfactual
- reproducibility
- confounders
- alternative explanations

But the policy engine decides whether the evidence satisfies a cause-confirmation gate.

## 24. Confidence language

AI prose should use calibrated qualitative language tied to state.

Preferred:

- "H1 currently has more support than H2, but contradiction C4 remains unresolved."
- "This finding weakens H2 because it fails a necessary prediction."

Avoid:

- "I'm 87% sure H1 is the root cause"

unless a domain-specific validated calibration model explicitly defines that percentage.

## 25. Conclusion drafting

The AI may draft a conclusion narrative only from authoritative state plus clearly labeled accepted residual uncertainty.

It must include:

- conclusion strength
- causal assertion(s)
- material evidence basis
- material alternatives considered
- unresolved uncertainty
- verification status

The narrative cannot upgrade the underlying conclusion state.

## 26. Proposal staleness

A proposal becomes stale when materially relevant inputs change.

Examples:

- target entity changed
- new contradiction arrived
- hypothesis was falsified
- policy changed
- domain version changed
- evidence source was invalidated

Stale proposals require revalidation/regeneration before acceptance.

## 27. Multi-model use

The architecture may use multiple AI providers or models.

Different models may be assigned roles such as:

- hypothesis generation
- adversarial challenge
- summarization
- evidence relationship proposal
- report drafting

Their outputs remain proposals under the same semantic rules.

## 28. Model disagreement

Model disagreement can be recorded as competing proposals or analysis evidence about reasoning behavior.

It is not itself proof about the underlying investigated system.

## 29. Independent model review

If a workflow uses independent-model review, the system should preserve:

- model/provider identity
- context snapshot/version
- prompt-policy version
- whether models saw each other's output
- order of review

This supports research into cross-model consistency.

## 30. Cost and latency

AI invocation should record when available:

- provider/model
- input/output usage
- monetary cost
- latency
- cache/reuse status
- task type

This allows later evaluation of investigation value per cost.

## 31. AI failure

If AI is unavailable:

- existing investigation state remains accessible
- deterministic capabilities still work
- manual evidence/hypothesis/action entry remains possible where supported
- no state corruption occurs
- pending AI proposals are not assumed complete

## 32. AI timeout/retry

Retrying an AI reasoning call may produce a different proposal.

AI retries are not idempotent semantic commands.

Each generated proposal should have unique provenance.

Acceptance into authoritative state remains separately idempotent.

## 33. Explain why

For material suggestions, the user should be able to request an explanation based on inspectable references.

The explanation should distinguish:

- evidence
- inference
- assumption
- policy blocker
- recommendation heuristic

## 34. Challenge mode

The product should support a deliberate challenge operation.

Challenge mode asks the model to:

- attack the leading explanation
- identify overlooked alternatives
- test evidence independence
- find scope mismatches
- identify hidden assumptions
- find circular reasoning
- propose falsifying tests
- identify what would change its view

Challenge output remains proposal/analysis, not authoritative state.

## 35. Research mode

For EDF and other methodology research, the AI should support frozen-context runs.

A research run records:

- immutable investigation/evidence version
- semantic/domain version
- policy version
- model/provider version
- prompt/instruction version
- tool availability
- output
- cost/latency

This allows repeatable comparisons.

## 36. Prompt/version registry

Material AI instruction sets should have stable identifiers and versions.

Changing prompt text in a way likely to affect behavior requires a new version.

Historical AI proposals remain attributable to their original prompt-policy version.

## 37. Domain pack guidance

Domain packs may add specialized AI guidance, but the host composes it under the generic contract.

EDF may add:

- EDF terminology
- experiment interpretation guidance
- metric definitions
- evidence-closure questions
- domain-specific unknowns

It cannot instruct the model to bypass generic provenance or authority rules.

## 38. No hidden ontology upgrade

The model must not infer that a historical record uses a newer EDF meaning simply because the current prompt knows the newer version.

Semantic identity/version controls interpretation.

## 39. UI relationship

Chat is an input/explanation surface.

Structured proposal cards, state regions, blockers, and domain views are the authoritative interaction surface for consequential state.

A chat message that says "root cause confirmed" does not confirm a root cause.

## 40. Test strategy

AI orchestration tests should include:

- observation/inference separation
- evidence citation completeness
- prompt-injection evidence
- stale proposal detection
- negative-knowledge avoidance
- leading-hypothesis challenge
- dependency-aware corroboration
- unsupported conclusion prevention
- domain-version mismatch
- model outage
- tool provenance
- bounded context omission behavior

## 41. Completion criterion

The AI architecture is healthy when replacing the model changes proposal quality but does not change:

- authoritative domain semantics
- transition legality
- persistence truth
- historical meaning
- extension compatibility

The model is a reasoning participant, not the system of record.


## 42. Competition-grade adversarial orchestration

The competition profile should follow [MULTI-AGENT-INVESTIGATION.md](MULTI-AGENT-INVESTIGATION.md).

### 42.1 Blind role execution
Specialized agents should receive the same frozen authoritative snapshot and should not see other agent outputs before their initial proposal set is recorded.

### 42.2 Role specialization
At minimum, the architecture should support specialized roles for hypothesis generation, skepticism, evidence auditing, experiment design, causal analysis, and verification auditing.

### 42.3 Dependency-aware agreement
If one model/agent saw or derived from another agent's output, the second output must not be represented as independent agreement.

### 42.4 Reconciliation
Reconciliation should focus on explicit disagreement and the evidence/action that could resolve it rather than forcing consensus.

### 42.5 Tournament runs
Frozen-state research runs may compare model/provider, prompt version, domain version, and policy version.

### 42.6 Experiment pre-registration
AI-assisted experiment design should support freezing predictions and interpretation criteria before result capture.

### 42.7 Quality diagnostics
AI may propose hidden assumptions, contradictions, evidence dependencies, circular-reasoning risks, missing alternatives, and orphan state. These remain proposals/derived diagnostics until accepted where authoritative state is required.


## 43. Proof-carrying proposal profile

Material AI proposals should follow [PROOF-CARRYING-PROPOSALS.md](PROOF-CARRYING-PROPOSALS.md).

The orchestration layer should preserve:

- exact evidence/entity dependencies
- assumptions
- semantic/policy versions
- falsifier/challenge condition
- expected state impact
- proposal-kind validation
- staleness dependencies

A proposal should become stale because a material dependency changed, not merely because an unrelated event incremented AggregateVersion.

## 44. Planner integration

The next-action planner should follow [INVESTIGATION-PLANNER.md](INVESTIGATION-PLANNER.md).

AI may generate candidate actions and qualitative tradeoff reasoning.

Deterministic domain/policy code supplies:

- legal actions
- blockers
- authorization
- known costs/risks
- predictions
- negative knowledge
- budget constraints

Planner output remains recommendation, not authority.


## 45. Governed tool execution

AI tool use must follow [TOOL-EXECUTION-GATEWAY.md](TOOL-EXECUTION-GATEWAY.md).

The model receives explicit tool descriptions/capabilities, never unrestricted system authority.

Tool outputs are Findings/data with provenance and are not automatically Evidence.

## 46. Bounded autonomous mode

Autonomous orchestration should follow [BOUNDED-AUTONOMOUS-INVESTIGATION.md](BOUNDED-AUTONOMOUS-INVESTIGATION.md).

Competition target:

- Level 1 read-only tool autonomy
- explicit budget
- challenge-before-confirmation
- human checkpoints for protected transitions
- loop prevention
- safe recovery

Autonomy is a continuation of the command/policy/tool architecture, not a bypass around it.
