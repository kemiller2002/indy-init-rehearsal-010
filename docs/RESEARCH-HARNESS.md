# Research Harness

Status: ambitious reusable experimentation platform.

The investigator should become a laboratory for evaluating investigation methodology, AI models, prompts, policies, and EDF itself.

## 1. Experiment identity

A ResearchExperiment records:

- ExperimentId
- question/hypothesis
- frozen source cases
- treatment definitions
- outcome measures
- randomization/blinding where used
- run configuration
- cost/time
- results
- interpretation
- limitations

## 2. Treatment dimensions

A treatment can vary:

- model
- provider
- prompt
- agent role
- number of agents
- reconciliation strategy
- domain-pack version
- policy version
- planner version
- UI projection
- context selection strategy

## 3. Frozen cases

A case binds:

- InvestigationId
- AggregateVersion
- semantic manifest
- domain/policy version
- allowed tools
- hidden ground truth if available for benchmark

## 4. Blinded evaluation

When practical:

- evaluator does not know model/provider
- proposal order randomized
- treatment labels hidden

This reduces brand/model expectation bias.

## 5. Outcome measures

Potential measures:

- correct causal mechanism identification
- time/actions to falsify wrong leader
- contradiction discovery
- hidden assumption discovery
- evidence-dependency discovery
- discriminating action quality
- proposal acceptance/edit/reject
- residual uncertainty quality
- verification completeness
- cost
- latency

Do not choose metrics solely because they are easy to maximize.

## 6. Ground-truth benchmarks

Synthetic/known cases can provide:

- known cause
- known misleading evidence
- known hidden assumption
- known causal set
- known measurement failure

Use for repeatable regression.

## 7. Open-world cases

Real investigations may not have certain ground truth.

Evaluate:

- process quality
- calibration
- evidence discipline
- review outcome
- later verification

Do not label uncertain real-world conclusions "wrong" without basis.

## 8. Treatment isolation

Each treatment gets:

- same source snapshot
- explicit configuration
- separate outputs
- no cross-treatment leakage unless experiment designs it

## 9. Reproduction

ResearchExperiment can be rerun later with:

- same case snapshot
- same prompt version
- same policy/domain version
- closest available model version

If exact external model version unavailable, record that limitation.

## 10. Regression gates

A new prompt/domain/policy version may be blocked from default promotion if it materially regresses agreed benchmark criteria.

## 11. EDF use

Research questions:

- does ContextCoverage variant improve discrimination?
- does EvidenceClosure reduce unsupported completion?
- does UnknownEffect representation reduce missed uncertainty?
- does negative knowledge reduce repeated work?
- does topology improve causal localization?
- do calibrated confidence methods improve decision quality?

## 12. Product use

Research questions:

- does multi-agent panel beat one strong model?
- does challenge mode reduce premature convergence?
- does hypothesis battlefield improve human decisions?
- does time travel improve trust/debugging?
- does proof-carrying proposal UI increase useful rejection/editing?

## 13. Artifact output

Generate a Folio experiment report:

- hypothesis
- methods
- treatments
- results
- limitations
- evidence
- decision posture

The report can feed Echelon research publishing.

## 14. Competition use

Show one compact real comparison:

```text
Single Agent
vs
Generator + Skeptic + Evidence Auditor

Same frozen case.
Different contradiction discovery.
Different cost.
Different next-test quality.
```

This shows the app as a research platform, not a one-off demo.
