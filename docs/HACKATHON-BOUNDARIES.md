# Hackathon Boundaries

This document exists to keep preparation aggressive while preserving the integrity of the hackathon experiment.

## What we intend to prepare before kickoff

We can prepare and repeatedly test:

- Ordo
- ROS
- Aegis
- Praxis
- Limen
- Forma
- Folio
- generic reusable libraries that are independently legitimate frameworks
- requirements
- acceptance criteria
- architecture
- state diagrams
- UX specifications
- threat model
- accessibility requirements
- test strategy
- build/deployment strategy
- experiment protocol
- demo plan
- sample investigation scenarios
- EDF research that informs the generic model

## What we intentionally do not prepare

Before kickoff, do not create a completed competition application and then reproduce it during the event.

The intended experiment is stronger if the application itself begins at zero.

## Repository separation

Before kickoff this repository is primarily a planning and evidence repository.

At kickoff, create the organizer-compliant implementation repository or implementation area and record:

- initial timestamp
- initial commit
- starting contents
- framework versions used
- requirements baseline
- environment information

## Why this matters

The core claim is not:

> We can reproduce an application we already built.

The stronger claim is:

> We can prepare and validate a reusable software factory, give it a detailed specification for a new application, and have it produce defensible software under an external time constraint.

## Organizer verification

Before the event, verify with the organizers any ambiguity involving:

- use of pre-existing private or public frameworks
- pre-written requirements
- pre-written tests or acceptance criteria
- generated scaffolding
- framework templates
- AI agent instruction files
- use of GitHub as the application datastore
- authentication infrastructure

If organizer guidance conflicts with this document, organizer guidance wins and the change should be recorded here.


## Published rule verification

Last reviewed: 2026-09-24.

The currently published Indy Init hackathon page states:

- the repository starts empty;
- teams may plan beforehand, including the problem, stack, and sketch;
- code starts at kickoff;
- teams may use any model/framework/platform;
- the challenge is to build a production-grade app and the software factory that ships it;
- teams demonstrate the machinery as well as the product;
- the build window is currently published as 9:30 AM to 5:00 PM on November 7, 2026.

Sources:

- https://indyinit.com/hackathon/
- https://indyinit.com/
- https://indyinit.com/faq/

These published statements support aggressive pre-event planning/specification, but they do not answer every implementation-boundary question.

Continue to verify ambiguity around:

- whether executable application-specific tests may exist before kickoff;
- whether application-specific machine-readable contracts count as planning or scaffolding;
- whether generated implementation templates may contain application-specific code;
- exact auth/deployment expectations;
- whether a separate planning repository may be consumed directly by the blank implementation repo.

Until clarified, keep this repository specification-heavy and keep competition application source code out of it.

## Machine-readable specifications

A machine-readable contract generated directly from these requirements may be extremely valuable for the software factory.

Before producing application-specific executable/generated scaffolding from it before kickoff, confirm organizer interpretation.

Architecture and semantic contracts themselves remain preparation artifacts.
