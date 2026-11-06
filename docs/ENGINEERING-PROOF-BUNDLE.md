# Engineering Proof Bundle

Status: competition and long-term engineering evidence contract.

The application should be able to prove how it was built, what it claims to satisfy, and what remains incomplete.

This is a first-class artifact of the software factory.

## 1. Bundle identity

Every proof bundle identifies:

- ProofBundleId
- repository
- branch
- commit/build id
- generation time
- environment
- framework versions
- semantic contract digest
- requirements baseline version

## 2. Competition provenance

Record:

- kickoff timestamp
- starting repository contents
- starting commit
- framework/scaffolding present at kickoff
- organizer-approved pre-event artifacts
- first implementation commit
- commit timeline

## 3. Requirements evidence

For each requirement:

- requirement ID
- status
- acceptance scenarios
- ROS work item
- implementation files
- tests
- runtime evidence
- known deviations

Status should distinguish:

- NotStarted
- InProgress
- ImplementedUnverified
- Verified
- Deferred
- Deviated

## 4. Acceptance evidence

For each acceptance scenario:

- scenario ID
- automated/manual
- test id
- result
- build/commit
- evidence artifact
- last verified time

## 5. Test evidence

Include summaries:

- unit
- property
- state-space
- integration
- accessibility
- security
- offline/concurrency
- visual regression
- mutation testing

Do not claim categories not actually run.

## 6. Praxis metrics

Capture:

- code metrics
- thresholds
- files/hunks with repeated modification
- test counts
- churn
- complexity/size where tracked
- build duration
- AI spend by task where available

## 7. ROS evidence

Capture:

- work items
- state transitions
- completion evidence
- unresolved obligations
- deviations
- agent identity where relevant

## 8. Aegis evidence

Capture:

- faults encountered
- fault classes
- resolved/unresolved
- regressions
- invariant violations caught

Do not include secrets or sensitive evidence payloads unnecessarily.

## 9. AI engineering provenance

For agent-generated code/work:

- provider/model
- task/work item
- prompt/instruction version where available
- branch/commit
- review/test evidence

Do not require private chain-of-thought.

## 10. Dependency inventory

List:

- Echelon components
- third-party dependencies
- versions
- licenses if relevant
- purpose
- known vulnerabilities where tooling supplies them

## 11. Semantic contract

Include:

- core semantic version
- command/event registry
- domain packs
- policy versions
- experimental profiles
- schema digests

## 12. Deployment evidence

Include:

- deployment target
- deployment workflow
- build id
- deployed commit
- smoke check
- demo health check result

## 13. Accessibility evidence

Include critical screen evidence:

- automated scan
- keyboard
- screen reader checks
- high zoom
- reduced motion
- mobile viewport

## 14. Security evidence

Include:

- secrets scan result
- CSP/security-header state where relevant
- prompt-injection tests
- unsafe rendering tests
- dependency scan where available
- auth approach

## 15. Known limitations

The bundle must explicitly list:

- unfinished requirements
- temporary hackathon shortcuts
- known defects
- unsupported browsers/devices
- unverified assumptions
- test gaps

This increases credibility.

## 16. Generated report

Folio renders a polished human-readable report.

The canonical machine-readable proof data remains separate.

## 17. Judge mode

Factory View may link directly into proof sections:

```text
Requirement P7.5
  -> A22
  -> test ConfirmCause_MaterialContradiction_Blocked
  -> commit abc123
  -> passing build 47
```

## 18. Immutable snapshot

Issued proof bundle is bound to exact build/commit.

A later build generates a new bundle.

## 19. Integrity

Optionally record digests for:

- proof manifest
- final artifact
- requirement baseline
- generated report

## 20. Long-term value

After the competition, the proof bundle becomes useful for:

- client delivery
- due diligence
- regulated environments
- engineering review
- reproducibility
- agent-generated change governance
