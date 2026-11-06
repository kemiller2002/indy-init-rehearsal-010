# Multi-Agent Adversarial Investigation

Status: ambitious reusable AI orchestration profile.

The purpose of multiple agents is not to create consensus by committee. It is to expose different failure modes and generate higher-quality discriminating work.

## 1. Roles

### Hypothesis Generator

Goal:
Generate materially distinct causal mechanisms.

Must provide:

- hypothesis proposal
- mechanism
- what it explains
- assumptions
- prediction
- strongest falsifier

### Skeptic / Red Team

Goal:
Attack the current leading explanation.

Must inspect:

- contradictory evidence
- hidden assumptions
- scope mismatch
- alternative causes
- instrumentation failure
- post-hoc predictions
- circular reasoning

### Evidence Auditor

Goal:
Assess evidence structure rather than conclusion.

Must inspect:

- provenance
- dependency/common source
- verification status
- supersession
- missing relationships
- apparent contradictions
- evidence/inference confusion

### Experiment Designer

Goal:
Propose tests that maximize discrimination while respecting cost/risk.

Must provide:

- target uncertainty
- hypotheses distinguished
- prospective predictions
- procedure
- expected outcomes
- cost/risk
- interpretation conditions

### Causal Analyst

Goal:
Assess causal mechanism and alternative explanations.

Must inspect:

- temporality
- mechanism
- intervention/counterfactual
- confounders
- causal sets
- contributing/trigger/enabling roles

### Verification Auditor

Goal:
Prevent premature closure after a correction.

Must inspect:

- corrective action target
- success criteria
- measurement plan
- regression risk
- verification evidence
- whether mitigation is being mistaken for correction

## 2. Blind phase

Each role receives:

- same frozen InvestigationId/AggregateVersion
- role-specific instruction version
- same domain/policy version
- only authorized tools
- no other agent output

Record:

- provider/model
- prompt version
- context manifest
- output
- cost/latency

## 3. Proposal normalization

Agent output is parsed into typed proposals.

Examples:

- ProposedHypothesis
- ProposedContradiction
- ProposedEvidenceDependency
- ProposedUnknown
- ProposedAssumption
- ProposedPrediction
- ProposedInvestigationAction

Free text is supporting explanation.

## 4. Cross-agent comparison

Derived comparison should identify:

- semantic duplicates
- unique proposals
- direct disagreements
- shared assumptions
- conflicting evidence citations
- missing evidence citations
- cost/latency

Do not reduce to majority vote.

## 5. Reconciliation round

Only after blind phase, selected agents may see competing outputs.

Tasks:

- critique specific proposal
- identify decisive evidence
- explain disagreement source
- propose discriminating action
- identify whether disagreement is semantic, evidentiary, or policy-based

## 6. Human review

User sees a panel such as:

```text
3 agents agree:
- H1 deserves testing

2 agents disagree:
- whether E7 contradicts H1

Unique finding:
- Evidence Auditor: E4 and E8 share the same source

Best discriminator:
- Experiment Designer: Test T3
```

User accepts/rejects individual structured proposals.

## 7. Diversity requirement

Using six identical prompts with six model calls is not a meaningful panel.

Roles must differ in objective and evaluation criteria.

Provider/model diversity is optional but desirable for research comparison.

## 8. Adversarial loop stop condition

Do not recurse agents indefinitely.

Possible stop conditions:

- no new material proposals
- no unresolved material disagreement
- budget reached
- user stops
- a discriminating real-world action is now required

The next best step may be "go collect evidence," not "ask more models."

## 9. Agent dependency tracking

If reconciliation agent R derives proposal P from Generator G, record that dependency.

Do not count R and G as independent support.

## 10. Model failure isolation

One invalid agent output:

- becomes failed analysis run
- does not block other roles
- does not corrupt state
- remains diagnosable

## 11. Tournament metrics

For each role/model, track:

- proposal acceptance rate
- proposal edit rate
- proposal rejection rate
- unique useful proposal count
- evidence citation precision
- contradiction discovery
- dependency discovery
- falsifier quality
- downstream state change
- cost
- latency

Avoid optimizing only for user acceptance, which can reward agreeable models.

## 12. Research question

Key long-term question:

> Does structured adversarial multi-agent reasoning improve investigation outcomes enough to justify additional cost and complexity?

The application should make that empirically testable.
