# Fresh-Agent Factory Acceptance

Status: factory acceptance specification.

The strongest pre-competition test is whether a new implementation agent can build a non-competition application correctly using only the initialized repository contract.

## Acceptance rule

A fresh agent passes only if it can independently determine:

- what product to build;
- which requirements are normative;
- which architecture choices are locked;
- which choices are reversible;
- what it must not decide;
- project/dependency boundaries;
- legal state transitions;
- how AI is allowed to participate;
- how persistence is separated;
- which screens must mean what;
- how success is tested;
- how completion evidence is recorded.

## Failure signals

Factory is not ready if the agent:

- asks where requirements are;
- asks whether UI may own state legality;
- invents a framework instead of using installed components;
- creates raw mutable JSON as authoritative state;
- puts GitHub types in Domain;
- treats AI output as state;
- omits mobile/accessibility semantics;
- declares completion without tests/evidence;
- cannot find current ROS work;
- cannot identify stop-the-line constraints;
- rebuilds capabilities that the factory already installed.

## Scorecard

Record each dimension as:

- Pass without intervention
- Pass after repository-contained clarification
- Human intervention required
- Failed

Dimensions:

1. Contract discovery
2. Architecture discovery
3. Work sequencing
4. Domain semantics
5. UI semantics
6. Persistence boundary
7. AI authority boundary
8. Security boundary
9. Test strategy
10. Completion/proof strategy

Competition target:
No Human intervention required in any foundational dimension.

## Improvement loop

Every human intervention produces:

1. missing/ambiguous factory contract;
2. owner component;
3. requirement;
4. implementation;
5. regression test/rehearsal check.

The objective is not to train one agent to know the project. The objective is to make the repository teach any capable agent what it needs to know.


## Paired-lane use

Fresh-agent acceptance is intended to be applied identically to independent agent lanes.

For the OpenAI/Codex and Claude rehearsal:

- do not relax criteria for one provider;
- do not let the second lane inspect the first;
- do not count a provider process exit as completion;
- do not count a self-authored evidence file as sufficient unless referenced paths exist and independent checks pass;
- classify foundational intervention as a factory failure signal even if the final repository eventually passes.

The competition-readiness target is not merely "both agents can eventually build it."

It is:

> Both agents can discover the intended product and engineering constraints from the repository itself, without foundational human translation.
