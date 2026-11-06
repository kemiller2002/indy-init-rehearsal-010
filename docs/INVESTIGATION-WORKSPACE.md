# Investigation Workspace Product Contract

Status: normative UX/product direction for the primary working surface.

The Investigation Workspace is the core product experience. It must let a user understand current investigation state, decide what to do next, and interact with AI assistance without requiring state-machine expertise.

## 1. Primary user question

At all times the workspace should help answer:

> What do we currently know, what could explain it, what conflicts with that explanation, and what should we do next?

## 2. Workspace principles

The workspace must:

- make structured state primary
- keep conversational input easy
- preserve competing hypotheses
- surface blockers and uncertainty
- make the next useful action obvious
- avoid forcing linear wizard behavior
- remain usable on iPad
- support narrow phone layouts
- preserve accessibility at high zoom
- show persistence truth

## 3. Core regions

The workspace contains semantic regions, not fixed desktop panels.

Required regions:

- Investigation Header
- Observation / Problem Statement
- AI Input / Ask
- Hypotheses
- Evidence
- Unknowns and Assumptions
- Contradictions
- Obligations / Blockers
- Next Actions
- Persistence State
- Timeline / History access

Secondary/conditional regions:

- Predictions
- Findings
- Causal model
- Corrective actions
- Verification
- Domain-specific projections
- Collaboration/review
- Report status

## 4. Investigation header

Must show:

- title
- lifecycle state
- domain pack/profile
- conclusion posture when applicable
- unresolved blocking count
- persistence status
- last authoritative change
- access to history/report/settings

Do not make domain-pack version the dominant label, but make it inspectable.

## 5. Observation region

The original problem statement remains easy to inspect.

If structured normalization exists, the UI distinguishes:

- original statement
- normalized/structured interpretation

Editing the normalized form must not rewrite original provenance.

## 6. AI input

The primary text input can ask:

- what happened
- add new information
- ask a question
- request analysis
- propose an action
- challenge the current conclusion

The input is not itself authoritative state.

The system parses it into proposed structured changes or responds with analysis/questions.

## 7. AI response shape

When AI output would change investigation state, show structured proposal units.

Examples:

```text
Proposed hypothesis
"Connection pool exhaustion is causing timeouts."

Why:
- E12 supports...
- O4 is consistent...

Depends on:
- A3: traffic profile is comparable

[Accept] [Edit] [Reject] [Why?]
```

Multiple proposed records may be grouped when they form one coherent suggestion, but each accepted authoritative entity retains distinct identity/provenance.

## 8. Hypotheses region

Each hypothesis card/row should expose:

- concise statement
- lifecycle state
- what it explains
- material support
- material contradiction
- unresolved unknown count
- assumption burden
- key prediction(s)
- whether it is currently leading
- available actions

Falsified hypotheses may collapse into history/secondary view but remain discoverable.

## 9. Hypothesis comparison

The workspace should support comparing active hypotheses by:

- predictions
- supporting evidence
- contradictory evidence
- unresolved unknowns
- assumptions
- discriminating next actions

Avoid reducing comparison to one unexplained score.

## 10. Evidence region

Evidence should be filterable by relationship/context.

Useful views:

- all recent
- supporting current hypothesis
- contradicting
- discriminating
- unlinked/uninterpreted
- unverified
- dependent/correlated
- superseded
- external artifact unavailable/changed

## 11. Evidence capture

Fast capture paths:

- type/paste observation
- paste log/text
- add URL reference
- attach/import supported artifact metadata
- record measurement
- record experiment result
- import through domain adapter

Capture should ask only required fields initially and allow progressive enrichment.

## 12. Evidence distinction

The UI must distinguish:

- source content
- AI interpretation
- user interpretation
- relationship to hypotheses

Do not embed interpretation so tightly into evidence presentation that users cannot tell them apart.

## 13. Unknowns

Blocking unknowns are primary work items.

Each should show:

- what is unknown
- why it matters
- what transition/work it blocks
- suggested resolution action
- owner/status when applicable
- whether policy permits residual acceptance

## 14. Assumptions

Material assumptions should be visible near dependent hypotheses/conclusions.

Users should be able to answer:

- what are we assuming?
- which conclusions depend on it?
- has it been verified?
- what happens if it is false?

## 15. Contradictions

Material contradictions should never be hidden solely in history.

Show:

- conflicting entities
- why the conflict matters
- materiality
- current resolution status
- proposed resolution actions

Resolved contradictions remain inspectable.

## 16. Obligations/blockers

The workspace should group outstanding work by purpose:

- blocks confirmation
- blocks resolution
- blocks persistence/sync
- requires review
- evidence needed
- verification needed

The UI should derive this from capability/blocker projection.

## 17. Next actions

The Next Actions region should emphasize a small number of useful actions.

Potential categories:

- answer question
- collect evidence
- run check
- compare hypotheses
- resolve contradiction
- verify assumption
- challenge leading hypothesis
- propose/verify correction

Each recommended action should explain why it is useful.

## 18. Recommended versus legal

A legal action is not necessarily recommended.

The UI should distinguish:

- Recommended
- Available
- Unavailable with reason

This prevents the policy engine from being mistaken for an AI prioritization engine.

## 19. Discoverable unavailable actions

Important blocked transitions such as Confirm Cause should remain discoverable.

Example:

```text
Confirm cause
Unavailable
2 blockers:
- unresolved contradiction
- required verification missing
```

Do not simply hide the button.

## 20. Timeline/history

History should expose:

- authoritative state changes
- accepted/rejected proposals as configured
- evidence admissions
- hypothesis changes
- policy/domain version changes
- reviews
- corrective/verification events
- sync/recovery events when material

History can be filtered but must preserve sequence.

## 21. Explain change

For a state transition, the user should be able to inspect:

- who/what changed it
- prior state
- new state
- reason
- evidence basis
- governing policy
- timestamp/sequence

## 22. Conversation history

Chat transcript is supporting context, not authoritative investigation history.

It may be retained separately according to product/privacy policy.

Accepted structured proposals are what matter to authoritative state.

## 23. Conclusion review

When a cause is near confirmation, provide a dedicated review surface containing:

- proposed causal assertion
- evidence basis
- contradiction status
- unknowns
- assumptions
- alternative explanations
- causal-support dimensions
- policy gate status
- required reviews

This is more important than a celebratory "AI found the root cause" screen.

## 24. Corrective action view

Once a cause is supported/confirmed, support:

- mitigation/workaround/correction/prevention distinction
- target cause/mechanism
- owner
- status
- verification criteria
- evidence of implementation
- evidence of verification

## 25. Verification view

Show criteria before outcome.

Each criterion should display:

- expected result
- measurement method
- scope
- result
- evidence
- pass/fail/unresolved
- verification actor

## 26. Persistence indicator

Persistence status must be visible without overwhelming normal work.

States:

- Saved/Synced
- Saving/Pending
- Offline
- Conflict
- Failed
- Revalidation required

A local-only change should be visibly distinguishable from remote confirmed state.

## 27. Conflict UI

Conflict resolution should explain semantic intent.

Example:

```text
Your pending action:
Confirm H1 as falsified

Remote change:
H1 was superseded after new evidence E19

Your action needs revalidation.
```

Do not show raw JSON merge unless a developer/debug view explicitly requests it.

## 28. Domain-pack degradation

If optional EDF UI is unavailable:

- show generic extension fallback
- preserve core workspace
- explain specialized view unavailable

If required semantics are missing:

- show safe read-only/degraded mode
- identify missing pack/version
- disable unsafe writes

## 29. Research comparison mode

For EDF research, allow side-by-side derived comparison of:

- policy versions
- domain-pack versions
- model runs
- experiment snapshots

Comparison mode must clearly identify the shared immutable source and must not mutate it.

## 30. Report access

The workspace should offer:

- interim report
- final report when legal
- report preview
- report version history

If unresolved work exists, interim report states it explicitly.

## 31. iPad landscape

Likely pattern:

- primary center: active hypotheses/evidence/next action
- supporting side region: obligations/unknowns or AI interaction
- responsive details via flyout

Exact layout is not locked.

Semantic visibility requirements are locked.

## 32. iPad portrait

Prioritize:

1. header/state
2. observation/current conclusion posture
3. next action/blockers
4. hypotheses
5. evidence/unknowns
6. secondary detail

Use tabs/accordions/flyouts only when they do not hide critical blockers.

## 33. Phone

Phone must support complete core investigation work.

Prefer:

- stacked semantic regions
- sticky current-state/action bar where appropriate
- full-screen detail views
- accessible bottom sheet/flyout only where Forma supports it correctly

No feature may require hover.

## 34. Desktop

Desktop may use more simultaneous regions.

It must not become a separate product or expose capabilities unavailable on tablet solely due to layout.

## 35. Keyboard and focus

All actions must be operable by keyboard.

After accepting/rejecting a proposal or completing an action:

- focus moves predictably
- result is announced appropriately
- no focus trap is introduced
- history/state update can be discovered

## 36. Screen reader

State changes should expose semantic announcements such as:

- "Hypothesis H2 changed to Falsified."
- "Confirm Cause unavailable: two blockers."
- "Evidence saved locally; synchronization pending."

Do not rely on icon/color alone.

## 37. Reduced motion

Animated state changes may clarify relationships but must respect reduced-motion preference.

Motion is never required to understand state.

## 38. High zoom

At high text zoom:

- blocking information remains visible
- no fixed-height content clips
- action labels remain usable
- evidence/proposal cards reflow
- horizontal scrolling is not required for primary investigation tasks

## 39. Empty states

Empty states should guide meaningful action.

Examples:

No hypotheses:
"Describe what happened or ask the investigator to propose possible explanations."

No evidence:
"Add what you observed, measured, or learned."

No blockers:
"No blocking obligations for the selected transition."

Avoid decorative emptiness.

## 40. Loading states

Loading must distinguish:

- loading local projection
- fetching remote changes
- waiting on AI
- retrieving external evidence

The user should be able to continue safe local work when possible.

## 41. Error states

Errors should use Aegis faults when applicable.

The UI should show:

- what failed
- whether work is preserved
- what user can do next
- correlation/reference if useful

Do not show generic "Something went wrong" as the only diagnostic.

## 42. First-run experience

The first screen should remain extremely simple.

Suggested:

```text
What happened?

[Describe the problem...]

[Start investigation]
```

After input, the app progressively reveals structure.

Do not front-load ontology or configuration unless required.

## 43. Domain selection

Domain pack can be:

- chosen explicitly
- inferred/proposed by AI
- defaulted by workspace

Inference is a proposal. User/workspace policy controls activation.

For the hackathon, generic mode may be sufficient.

## 44. Investigation creation flow

1. record original statement
2. create Draft investigation
3. AI proposes structured observation(s), unknowns, initial hypotheses
4. user accepts/edits/rejects
5. investigation enters Active when minimum required state exists
6. workspace opens on first discriminating next action

## 45. Quick add

From anywhere in an active investigation, allow quick capture of:

- observation
- evidence
- note/comment
- unknown
- hypothesis
- finding

The resulting category must be explicit or proposed for confirmation.

## 46. "I learned something" flow

User input:
"I checked the database and rows are missing before the export job starts."

Expected behavior:

- preserve original text
- propose Observation/Finding
- identify affected hypotheses
- propose evidence relationships
- identify newly falsified/weakened hypotheses
- show changed next action
- user controls authoritative acceptance where required

## 47. "Why can't I?" flow

When a user attempts an illegal action:

- do not merely disable
- show blocker explanation
- link to blocking entities
- offer legal resolution actions

## 48. "Challenge this" flow

From a hypothesis/conclusion:

- launch AI challenge operation
- freeze current relevant state/version
- return overlooked alternatives/assumptions/falsifying actions
- show outputs as proposals
- preserve model provenance

## 49. "What next?" flow

AI response should be grounded in:

- blockers
- discriminating value
- unresolved contradictions
- leading hypothesis challenge
- action cost/risk

It should return one primary and a small number of alternatives, not an overwhelming list.

## 50. Review flow

Before high-consequence transitions:

- show transition summary
- show blockers cleared
- show evidence basis
- show residual uncertainty
- show policy/review requirements
- require explicit confirmation if policy requires

## 51. Collaboration

Where collaboration is enabled:

- show actor attribution
- show assignments
- show pending reviews
- avoid live collaborative editing assumptions unless implementation supports them safely
- use optimistic concurrency for authoritative changes

## 52. Notifications

Notifications should be used for:

- review requested
- assigned obligation/action
- persistence conflict
- external evidence arrived
- verification due/failed
- investigation reopened

Notification infrastructure is later deployment work, not core semantic state.

## 53. Accessibility as contract evidence

Each consequential screen requires:

- semantic landmarks
- correct labels
- focus order
- keyboard path
- screen-reader state exposure
- contrast
- text scaling
- reduced motion
- error association
- status announcements

Automated accessibility checks alone are insufficient.

## 54. UI analytics

If product analytics are enabled, they should measure task behavior without becoming authoritative investigation history.

Useful metrics:

- time to first structured hypothesis
- time to first discriminating action
- blocker resolution time
- proposal accept/edit/reject
- evidence capture friction
- conflict recovery success
- report generation

Analytics must respect deployment privacy policy.

## 55. First-release screen set

The minimum durable product should implement:

1. Investigation Start
2. Investigation Workspace
3. Evidence Capture/Detail
4. Hypothesis Detail/Compare
5. Action/Experiment Definition
6. Finding/Result Capture
7. Unknown/Assumption/Contradiction Detail
8. Cause Review
9. Corrective Action/Verification
10. History
11. Report Preview
12. Persistence Conflict/Recovery

Not every screen must be a separate page layout on small devices, but every screen keeps its own canonical URL ([Screen Contracts](SCREEN-CONTRACTS.md#url-contract)), so a copied link opens the same view on any device.

## 56. Completion criterion

The workspace is successful when a user can answer, without reading chat history:

- what happened?
- what explanations are active?
- what evidence matters?
- what contradicts the leading explanation?
- what remains unknown?
- what is assumed?
- what has been ruled out?
- what should I do next?
- what actions are blocked and why?
- is my work actually saved?


## 57. Advanced competition surfaces

The competition profile should add advanced projections from [ADVANCED-VISUALIZATIONS.md](ADVANCED-VISUALIZATIONS.md).

### 57.1 Time-travel control
History should support reconstruction/scrubbing to an exact aggregate version with current version clearly distinguishable.

### 57.2 Semantic diff
Users should compare versions through meaning changes rather than raw object/text diff by default.

### 57.3 What changed
Major posture/capability changes should offer a "Why did this change?" action tied to actual events/evidence/policy.

### 57.4 Hypothesis Battlefield
A dense comparison surface should expose strongest support, strongest contradiction, assumptions, unknowns, predictions, negative knowledge, and falsifying actions.

### 57.5 Evidence dependency
Evidence lineage/common-source dependency must have both graph and accessible hierarchical/table projections.

### 57.6 Counterfactual mode
Sandbox mode must be visually unmistakable from authoritative state and identify source version and hypothetical modifications.

### 57.7 Multi-agent panel
Agent roles, provider/model identity, agreements, disagreements, unique proposals, evidence citations, cost, latency, and human dispositions should be inspectable.

### 57.8 Replay
Replay must derive from authoritative events and support pause, step, deterministic sequence, reduced motion, and screen-reader summaries.

### 57.9 Spectator mode
Read-only presentation mode should surface problem, hypotheses, evidence, blocker, next action, causal view, persistence state, and live/replayed event stream.

### 57.10 Factory view
A separate competition surface should show real engineering provenance and traceability without being confused with investigation semantic state.
