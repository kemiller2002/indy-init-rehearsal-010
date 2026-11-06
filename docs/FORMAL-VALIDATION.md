# Formal and State-Space Validation Profile

Status: ambitious engineering verification profile.

The competition should push beyond example-based tests. The state-directed model gives us an opportunity to systematically explore legal and illegal behavior.

## 1. Structural state-space explorer

For each lifecycle:

- enumerate states
- enumerate applicable commands
- evaluate structural transition rules
- verify every state/command pair has a defined outcome

Output matrix:

- Allowed structurally
- Invalid lifecycle
- Requires payload/entity
- Extension-owned

## 2. Policy state-space sampling

Full Cartesian exploration may be enormous.

Generate meaningful combinations of:

- blocking unknown present/absent
- contradiction present/resolved
- required assumption verified/unverified
- required evidence present/absent
- review present/absent
- actor permission present/absent

For protected transitions:

- ConfirmCause
- ResolveInvestigation
- AcceptResidualUncertainty
- MarkCorrectiveActionVerified

Prove expected blocker composition.

## 3. Property-based tests

Properties:

### Event replay determinism
Same event stream => same state.

### Prefix consistency
Projecting first N events from a stream equals time-travel state at N.

### Version monotonicity
Committed aggregate versions strictly increase.

### No silent deletion
Entities affecting history remain reconstructable after supersession/falsification.

### Extension preservation
Unknown optional extension survives encode/decode round trip.

### Cause confirmation safety
Any Confirmed causal assertion satisfies active core gate invariants.

### Resolution safety
Any Resolved investigation satisfies resolution invariants.

### Proposal authority safety
No Proposal event alone changes target authoritative entity state.

### Provider independence
Domain serialization has no provider metadata requirement.

## 4. Command/event round-trip property

For each allowed command:

```text
state S
 + command C
 -> events E
 -> evolve(S,E)
 -> S2
```

Then capability/state assertions should match the command contract.

## 5. Event-stream fuzzing

Generate valid event streams from the transition model.

Then perturb:

- duplicate EventId
- missing version
- reversed event
- unknown required extension
- malformed payload
- version gap
- wrong InvestigationId

Loader must fail safely.

## 6. Serialization fuzzing

Fuzz:

- unknown fields
- missing optional fields
- large text
- Unicode
- unusual timestamps
- extension envelopes
- future optional semantic IDs

Compatible readers must preserve what they promise to preserve.

## 7. Mutation testing

Deliberately mutate critical policy/domain rules:

- remove blocking unknown rule
- allow Falsified -> Supported
- treat Implemented as Verified
- allow ConfirmCause with contradiction
- drop evidence dependency

Required tests should fail.

This proves tests protect semantics rather than merely execute code.

## 8. Policy mutation score

Praxis may track:

- protected mutations attempted
- mutations killed by tests
- surviving semantic mutations

A surviving critical semantic mutation is a stop-the-line defect.

## 9. Model-based UI testing

Generate UI test states from domain states/capabilities.

For each capability state:

- Available => action enabled
- Blocked => action visible/unavailable with reasons
- ReviewRequired => review state visible
- Unsupported semantics => degraded/read-only treatment

This reduces hand-crafted UI state gaps.

## 10. Offline chaos testing

Simulate:

- network drops before append
- network drops after append before acknowledgement
- remote advances while offline
- auth expires
- duplicate retry
- rate limit
- GitHub unavailable
- local restart
- corrupted local queue item

Verify:

- no lost pending intent
- no duplicate semantic events
- explicit status
- safe revalidation

## 11. Concurrency simulation

Simulate independent clients:

### Commutative case
A adds E1, B adds O2.

Expected:
both may survive with distinct provenance.

### Semantic conflict
A falsifies H1, B confirms causal assertion relying on H1.

Expected:
B revalidates/blocks.

### Policy change
A queues under P1, remote upgrades to P2.

Expected:
revalidation under applicable pin/migration rules.

## 12. AI contract fuzzing

Feed structured proposal parser:

- missing evidence refs
- unknown proposal semantic id
- invalid enum
- extra malicious instruction field
- oversized payload
- stale semantic manifest

Must fail closed or become non-authoritative analysis.

## 13. Prompt-injection corpus

Maintain adversarial evidence examples:

- direct override instruction
- fake system tag
- fake tool output
- encoded instruction
- markdown link injection
- nested quoted prompt

Test that evidence remains data.

## 14. Accessibility state matrix

For critical screens, generate states:

- no blockers
- one blocker
- many blockers
- conflict
- offline
- AI unavailable
- unsupported semantic pack
- long evidence text
- high zoom
- screen-reader use

Verify semantic actions remain discoverable.

## 15. Performance stress profiles

Synthetic investigations:

- 10 hypotheses / 100 evidence
- 100 hypotheses / 1,000 evidence
- 10,000 events
- deep evidence dependency chains
- many optional extensions

Measure:

- load projection
- capability evaluation
- semantic diff
- time travel
- search
- graph projection

Set competition targets after implementation baseline, not arbitrary now.

## 16. Replay equivalence

Compare:

- full replay from event 1
- snapshot at N + replay N+1..M

Resulting state at M must be equivalent.

## 17. Cross-provider equivalence

Export from GitHub provider and import to in-memory/file provider.

Event/state projection must remain equivalent.

## 18. Domain-pack isolation tests

Run core test suite:

- generic only
- EDF pack
- EDF experimental profile
- test dummy pack

Core invariants must pass unchanged.

## 19. Research regression suite

When EDF policy/semantics change:

- rerun fixed frozen cases
- compare semantic diff
- flag unexplained changed outcomes
- require rationale for intentional changes

## 20. Proof output

Validation run should produce a machine-readable result:

```text
ValidationRunId
BuildId
CoreSemanticVersion
PolicyVersions
Tests
Properties
Mutations
StatePairs
Failures
Duration
Artifacts
```

Folio proof bundle can summarize it.

## 21. Competition display

A compact validation dashboard can show real numbers:

- lifecycle state-command pairs explored
- property checks
- fuzz cases
- semantic mutations killed
- offline chaos cases
- accessibility states
- replay equivalence

No hard-coded vanity metrics.
