# Cross-Investigation Intelligence

Status: ambitious long-term reusable capability.

Prior investigations are valuable, but reusing them carelessly creates confirmation bias and false evidence.

## 1. Principle

A prior investigation may inform what to consider.

It does not prove what happened in the current investigation.

## 2. Similarity retrieval

Find prior investigations based on:

- observation semantics
- system/domain
- symptoms
- hypotheses
- causal mechanisms
- environment/version
- evidence patterns
- corrective actions

Similarity is a retrieval aid, not evidence.

## 3. Prior hypothesis reuse

A prior root cause may be proposed as a current Hypothesis.

It enters with provenance:

```text
Suggested because Investigation I-42 had similar observations.
```

It does not enter as current Evidence.

## 4. Prior negative knowledge

Scoped negative knowledge may be surfaced when:

- same version/environment/scope plausibly applies
- applicability can be explained

Never universalize a prior falsification automatically.

## 5. Reusable experiments

Prior investigation actions may become templates.

Reuse:

- procedure
- prediction structure
- cost estimate
- known risks
- reproducibility notes

Predictions for current investigation must be newly bound to current hypotheses/scope.

## 6. Recurrence detection

Identify repeated:

- causal mechanisms
- contributing factors
- corrective failures
- verification failures
- assumptions
- unknowns

This supports organizational learning.

## 7. Correction effectiveness

Across related investigations, ask:

- did this correction recur?
- was verification durable?
- did a workaround become permanent?
- did a preventive action reduce analogous failures?

Cross-case conclusions remain derived analytics.

## 8. Case clusters

Derived clustering may group investigations by:

- symptom
- cause
- mechanism
- component
- team/workflow
- domain pack

Cluster labels are analytical projections.

## 9. Anti-confirmation-bias mode

When a similar prior case exists, also retrieve:

- similar cases with different causes
- prior hypotheses that looked similar but were falsified

This prevents "last incident syndrome."

## 10. Knowledge cards

Reusable knowledge artifacts may summarize:

- known mechanism
- applicability scope
- evidence required
- known counterexamples
- verification patterns
- source investigations

Knowledge card is not Evidence until current-case evidence supports applicability.

## 11. Privacy boundary

Cross-investigation retrieval must obey:

- workspace
- client
- classification
- permissions
- domain policy

AI cannot use unauthorized prior investigations as hidden context.

## 12. Competition use

If sample cases exist, show:

"Two prior investigations looked similar. One was cache invalidation, one was bad source data. Here is the test that distinguishes them."

That demonstrates learned investigation strategy without pretending history is proof.
