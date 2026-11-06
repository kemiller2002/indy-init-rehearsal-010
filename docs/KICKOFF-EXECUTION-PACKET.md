# Kickoff Execution Packet

Status: canonical human/agent operating packet for competition kickoff.

This packet is intentionally concise. The machine-readable source is `kickoff/indy-init.kickoff.json`.

## 1. 9:30 baseline

Before application implementation:

1. verify target implementation repository is empty/organizer-compliant;
2. capture timestamp and repository HEAD/tree;
3. capture workstation/runtime versions;
4. pin the exact planning repository commit;
5. run Conditor plan;
6. inspect planned mutations;
7. run Conditor init;
8. run Conditor verify/doctor;
9. record generated lock and component versions;
10. create first post-bootstrap commit;
11. create/start initial ROS work item;
12. capture Praxis baseline.

No application feature coding begins before this evidence exists.

## 2. Canonical contract entry

Agents start with:

`kickoff/indy-init.kickoff.json`

Then required-before-coding documents in the manifest.

Agents must not infer architecture from README summaries when a normative contract exists.

## 3. Work order

1. semantic kernel;
2. policy/capabilities;
3. semantic compiler structural checks;
4. workspace;
5. local durability;
6. GitHub provider;
7. AI proposals;
8. complete investigation/correction/verification loop;
9. time travel + semantic diff;
10. factory/proof view;
11. stretch features.

## 4. Parallel ownership after kernel stabilizes

### Domain agent
Owns:

- entities/types;
- commands/events;
- decide/evolve;
- policies/capabilities;
- state-space/property tests.

May not:

- introduce provider/UI/AI dependencies.

### UI agent
Owns:

- Limen boundary;
- Forma composition;
- screen contracts;
- URL routes: every screen, investigation, evidence item, branch and version addressable by a canonical hash URL, `.echelon/routes.json`, and pure round-trip-tested route parsing and formatting (R9.6–R9.10, SCREEN-CONTRACTS URL contract, Praxis SAF-URL-1..10);
- accessibility/responsive evidence.

May not:

- implement independent transition legality.

### Persistence agent
Owns:

- local durable store;
- GitHub provider;
- synchronization/conflict;
- canonical serialization.

May not:

- introduce GitHub identity into domain types.

### AI/Tool agent
Owns:

- context builder;
- proposal schemas;
- provider adapters;
- challenge/planner;
- tool gateway.

May not:

- write authoritative state directly.

### Factory/proof agent
Owns:

- semantic compiler;
- generated validation;
- Praxis/Aegis/ROS aggregation;
- proof bundle;
- demo health.

## 5. Integration rule

A branch/work item is integration-ready only when:

- target requirements identified;
- relevant acceptance tests pass;
- architecture boundary checks pass;
- no unresolved Aegis invariant fault;
- Praxis thresholds not silently exceeded;
- Percepta contract evidence exists for UI work;
- ROS attribution exists;
- known deviation explicitly recorded.

## 6. Cut order

If behind, cut:

1. visual polish beyond accessibility;
2. research tournament;
3. multi-agent roles beyond Generator/Skeptic/Evidence Auditor;
4. sensitivity variants;
5. cross-investigation intelligence;
6. dynamic graph polish;
7. bounded autonomy.

Do not cut:

- typed semantic kernel;
- capability/blocker integrity;
- original-source preservation;
- local persistence truth;
- AI proposal authority boundary;
- causal gate;
- corrective verification;
- security essentials;
- factory proof.

## 7. Checkpoints

### 10:30
Domain kernel and protected-transition test working.

### 11:15
Manual local investigation UI works.

### 12:00
Durable local + GitHub persistence path established or explicit fallback active.

### 12:45
Structured AI proposal loop works.

### 2:00
End-to-end cause -> correction -> verification -> resolution path works.

### 2:45
Time travel/semantic diff works.

### 3:30
One ambitious stretch capability complete.

### 4:00
Factory/proof view usable.

### 4:25
Security/resilience smoke pass complete.

### 4:40
Feature freeze except critical fixes.

### 4:55
Final proof bundle and deployment identity captured.

## 8. Stop-the-line

Immediately stop feature work for:

- false synced/persisted state;
- AI bypass of commands/policy;
- UI-owned legality;
- dropped unknown extension/history;
- hidden material blocker;
- verification collapsed into implementation;
- provider-specific generic semantics;
- secret exposure;
- screen contract accessibility failure;
- test/proof data being fabricated or hard-coded.

## 9. Completion definition

A feature is not complete because it renders.

It is complete when:

`requirement -> work item -> implementation -> test -> runtime/verification evidence -> proof trace`

exists, or an explicit deviation says why it does not.

## 10. Final freeze

At final freeze:

- stop feature branches;
- run full build/tests;
- run semantic compiler/contract validation;
- run demo health;
- run critical accessibility/security checks;
- generate exact-build proof bundle;
- record deployed commit;
- tag/freeze submission;
- preserve local fallback/spectator state.
