# Bounded Autonomous Investigation

Status: ambitious reusable orchestration profile.

The platform may support an autonomous mode where an agent continues investigation work within explicit budgets, capabilities, and stop conditions.

This is not unrestricted autonomy.

## 1. Autonomous session

```text
AutonomousSession
- SessionId
- InvestigationId/source version
- Actor/Agent identity
- Goal
- Budget
- Allowed tool capabilities
- Allowed command classes
- Human-review gates
- Stop conditions
- Domain/policy version
```

## 2. Loop

```text
Observe current state
  -> identify blocker/uncertainty
  -> propose action
  -> check capability/policy
  -> execute allowed tool or request human
  -> capture finding
  -> propose/admit evidence according to policy
  -> recompute state
  -> challenge leader
  -> continue or stop
```

## 3. Allowed autonomy levels

### Level 0: Analysis only
May generate proposals only.

### Level 1: Read-only investigation
May invoke authorized read/pure-analysis tools automatically.

May not make authoritative state changes except recording its own proposals/run artifacts.

### Level 2: Low-risk semantic automation
May auto-accept configured low-risk deterministic records, e.g. tool findings/evidence metadata, under explicit policy.

### Level 3: External reversible actions
Requires explicit deployment policy/review and is future-facing.

### Level 4: High-consequence remediation
Not implied by this architecture. Requires separate requirements.

Competition target should be Level 1, optionally narrow Level 2.

## 4. Budget

Session budget may constrain:

- wall-clock duration
- AI monetary spend
- model calls
- tool calls
- external queries
- human interruptions
- data volume

## 5. Stop conditions

Stop when:

- human review required
- side-effect tool required but unauthorized
- budget exhausted
- no useful legal action
- all candidate actions low information value
- cause eligible for human confirmation
- contradiction cannot be resolved automatically
- semantic/policy incompatibility
- tool/provider unavailable without fallback
- user stops

## 6. Human checkpoints

Checkpoint should summarize:

- what changed
- evidence collected
- hypotheses changed
- blockers resolved/created
- spend
- next proposed action
- why human is required

## 7. No silent high-consequence transition

Even in autonomy, configured protected transitions can require human confirmation:

- AcceptResidualUncertainty
- ConfirmCause
- approve corrective action
- ResolveInvestigation
- external side-effect action

## 8. Autonomous challenge

Before requesting ConfirmCause, autonomous session should run a challenge pass:

- strongest alternative
- falsifier
- hidden assumption
- evidence dependency
- unresolved contradiction

Policy may require this as obligation.

## 9. Loop prevention

Prevent unproductive cycles:

- repeated same action/input
- repeated previously falsified path
- repeated identical model proposal
- tool retry without new condition
- oscillating hypothesis states

Use negative knowledge/run history.

## 10. Session evidence

Store session trace at safe semantic level:

- actions proposed/executed
- tools
- structured proposals
- decisions/blockers
- costs
- findings/evidence
- checkpoints

Do not require hidden chain-of-thought.

## 11. Recovery

If session crashes:

- authoritative investigation remains valid
- completed tool invocations/findings remain attributable
- pending work recoverable
- session can resume from current state after revalidation

## 12. Competition moment

Click:

```text
Investigate autonomously for 2 minutes
Budget: $0.50
Tools: GitHub read + local analysis
Protected: ConfirmCause requires me
```

The system performs bounded real work, then stops with:

```text
I found a configuration difference supporting H2.
I ruled out H1 for this scope.
One contradiction remains.
I need you before continuing because the next test touches production.
```

That demonstrates genuine agentic capability plus governance.
