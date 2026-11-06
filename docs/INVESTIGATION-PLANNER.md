# Investigation Planner

Status: ambitious reusable decision-support profile.

The planner chooses useful next actions without pretending there is one universal numerical objective.

## 1. Planner inputs

The planner consumes:

- active hypotheses
- predictions
- blocking unknowns
- contradictions
- assumptions
- negative knowledge
- legal capabilities
- candidate actions
- action cost
- time
- reversibility
- operational risk
- urgency
- required permissions
- domain policy

## 2. Planner output

Return:

- primary recommended action
- small number of alternatives
- why primary is preferred
- which hypotheses/unknowns it discriminates
- expected outcome branches
- cost/risk caveats
- conditions that would change recommendation

## 3. Hard constraints

Never recommend an action that is:

- semantically illegal
- unauthorized
- unsupported by required semantics
- above configured risk boundary without required review
- already ruled out by applicable negative knowledge unless reopening basis exists

## 4. Qualitative objective dimensions

Candidate action evaluation may include:

- Discrimination
- BlockerResolution
- ContradictionResolution
- AssumptionValidation
- InformationBreadth
- Cost
- Time
- Reversibility
- OperationalRisk
- Safety
- Urgency
- Reproducibility

Do not collapse into one score unless a domain-specific validated policy defines it.

## 5. Pareto frontier

When actions trade off dimensions, expose a small Pareto-like set:

```text
Fastest useful
Cheapest useful
Most discriminating
Safest
Best overall under current policy
```

"Best overall" is a planner heuristic, not an authoritative truth, and must explain tradeoffs.

## 6. Action tournament

For top candidates:

- compare predicted outcomes across hypotheses
- identify shared/non-discriminating outcomes
- penalize tests where every hypothesis predicts same result
- identify tests that can falsify the leader

## 7. Blocking-first mode

If a transition is blocked by one decisive unknown/contradiction, prioritize resolving it unless:

- cost/risk excessive
- user has more urgent operational objective
- policy permits accepted residual uncertainty

## 8. Falsification-first mode

When one hypothesis dominates too early:

- search for cheap/safe falsifier
- surface alternative mechanism
- inspect measurement failure
- inspect source dependency

## 9. Operational mode

In incidents, the best immediate action may be mitigation rather than epistemic discrimination.

Planner must distinguish:

- stop harm now
- learn cause
- verify correction

It may recommend parallel tracks.

## 10. Parallel action planning

Some actions can run concurrently.

Represent:

- dependencies
- exclusivity
- shared resources
- required order
- parallel-safe actions

Planner may propose a small execution plan, not only one action.

## 11. Investigation budget

Optional budget:

- maximum time
- monetary cost
- operational risk
- AI spend
- human effort

Planner can say:

"Under the remaining budget, T3 is the highest-discrimination feasible test."

Budget itself is user/domain policy, not epistemic truth.

## 12. Stop recommendation

Planner may recommend stopping when:

- remaining uncertainty is explicitly acceptable
- next evidence cost exceeds configured value
- risk unacceptable
- all feasible tests non-discriminating
- policy permits closure without confirmed cause

It must not invent certainty to justify stopping.

## 13. What changes recommendation

Expose recommendation sensitivity:

- if A2 becomes false -> choose T5
- if E7 invalidated -> choose T2
- if outage severity rises -> mitigation M1 before experiment

## 14. Planner provenance

Record:

- source investigation/version
- candidate action set
- policy/domain version
- heuristic/planner version
- AI model if AI assisted

## 15. Deterministic + AI hybrid

Deterministic layer:

- legality
- blockers
- costs/risks known
- predictions recorded
- negative knowledge
- policy constraints

AI layer:

- generate candidate actions
- reason about discrimination
- explain tradeoffs
- propose hidden assumptions

Final output remains recommendation, not authority.
