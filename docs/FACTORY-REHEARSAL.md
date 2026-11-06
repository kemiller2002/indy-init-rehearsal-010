# Factory Rehearsal Protocol

Status: pre-competition validation protocol.

The purpose of rehearsal is not to pre-build the competition application. It is to prove that the software factory can take a fresh agent and an empty repository from specification to a governed working system with minimal human translation.

## 1. Sacrificial application

Use a different problem from the competition app.

Recommended rehearsal target:

**Incident Evidence Triage Board**

A small application that:

- accepts an incident statement;
- captures observations and evidence;
- supports three candidate explanations;
- tracks one blocking unknown;
- has one legal-transition gate;
- stores state through a provider abstraction;
- exposes one mobile workspace;
- produces a report.

It intentionally exercises the same factory seams without reproducing the Root-Cause Investigator's full product.

## 2. Rehearsal question

Can a fresh agent, given only:

- an empty Git repository;
- Conditor;
- the rehearsal manifest;
- installed Echelon components;
- machine-readable screen/domain contracts;

produce the intended application without architecture coaching?

## 3. Fresh-agent rule

The implementation agent must not receive an oral/private briefing.

Allowed inputs:

- initialized repository;
- AGENTS/project contract generated/materialized by the factory;
- normative requirements;
- ROS work;
- tool access normally available during competition.

If the agent asks a question whose answer already exists in the contract, record that as a factory-context failure.

If the agent invents an answer to a documented constraint incorrectly, record it as a contract/discoverability failure.

## 4. Measure

Track:

- bootstrap elapsed time;
- number of manual commands;
- number of files manually copied;
- clarification questions;
- architectural questions;
- invalid framework usage;
- requirements missed;
- UI contract violations;
- illegal states/transitions introduced;
- test failures;
- repeated file/hunk churn;
- Aegis faults;
- agent/model cost;
- first clean build time;
- first usable UI time;
- first persisted end-to-end flow;
- deployment time;
- proof-bundle completeness.

## 5. Failure classification

Every failure becomes one of:

- Conditor bootstrap gap
- manifest/context gap
- ROS sequencing gap
- Ordo semantic/requirement gap
- Percepta UI-contract gap
- Forma component gap
- Limen integration gap
- Aegis diagnostic gap
- Praxis metric/proof gap
- Folio report gap
- agent-specific failure
- external/tooling failure

Do not merely fix the sacrificial app. Fix the reusable factory layer whenever the failure is generic.

## 6. Rehearsal passes

### Pass 1: clean-room setup

Goal:
Empty repo -> initialized governed project.

Success:

- no manual copy/paste;
- all required components verified;
- pinned specification materialized;
- initial ROS/mission state exists;
- buildable scaffold exists;
- baseline proof captured.

### Pass 2: fresh-agent implementation

Goal:
Agent builds the sacrificial app using the factory contract.

Success:

- no architecture coaching;
- agent follows dependency direction;
- UI follows screen contract;
- legal actions derive from domain;
- persistence/provider separated;
- tests/evidence produced.

### Pass 3: adversarial review

A second fresh agent receives only repo state and is asked:

- find requirement gaps;
- find semantic violations;
- find security issues;
- find UI meaning violations;
- find evidence/proof gaps.

Generic findings become factory requirements.

### Pass 4: destructive rehearsal

Delete the implementation repo and repeat from empty.

Success:
The second run should require fewer interventions and produce less churn.

## 7. Targets

Pre-competition target:

- zero manual file copying;
- zero undocumented architecture decisions;
- zero required clarification questions whose answer is already in the manifest;
- one-command or one-entry-point bootstrap;
- deterministic scaffold;
- requirements and UI contract discoverable in first agent context;
- all factory-generated files attributable;
- second run materially faster/cleaner than first.

## 8. Evidence

Save each rehearsal as a run record containing:

- Conditor version/commit;
- manifest digest;
- component versions;
- target repo start/end commits;
- agent/provider/model;
- elapsed times;
- questions/interventions;
- ROS/Praxis/Aegis evidence;
- test/build/deploy results;
- lessons and resulting factory changes.

## 9. Competition readiness gate

Do not declare the factory ready merely because Conditor installs successfully.

Ready means:

> A fresh agent can correctly discover what to build, where to put it, which decisions are already made, how UI meaning is constrained, how completion is proven, and what it is forbidden to improvise.


## 10. Paired blind rehearsal

After the deterministic bootstrap smoke passes, run the sacrificial application independently with OpenAI/Codex and Claude.

Requirements:

- separate empty repositories;
- identical pinned factory/preset;
- no sibling-lane visibility;
- no oral/private architecture briefing;
- same independent scoring;
- retain failures for analysis.

The paired harness lives in Conditor:

```bash
bash scripts/rehearse-agent-pair.sh
```

A pair run is successful only if each lane independently satisfies fresh-agent acceptance. The comparison artifact is research evidence, not permission to average away a failure in one lane.

The most important output is the set of factory changes required to make both lanes more deterministic on the next clean-room run.
