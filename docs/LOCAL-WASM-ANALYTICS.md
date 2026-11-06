# Local WASM Investigation Analytics

Status: ambitious Limen/local-compute profile.

The browser should use WebAssembly for deterministic, privacy-preserving, low-latency investigation work where appropriate.

## 1. Principle

Do locally what does not require a remote model or service.

Benefits:

- privacy
- speed
- offline capability
- reproducibility
- reduced AI cost
- deterministic behavior

## 2. Candidate local analytics

### Semantic/event
- event replay
- snapshot validation
- semantic diff
- capability projection
- state-space checks
- branch projection

### Evidence
- hashing
- text diff
- simple parsing
- artifact metadata
- source-lineage graph
- cycle detection
- duplicate detection

### Search
- local full-text index
- entity filtering
- negative-knowledge lookup
- semantic metadata query

### Planning support
- blocker graph
- action dependency graph
- Pareto filtering over known numeric/categorical dimensions

### Reports
- projection preparation
- deterministic tables/glossary
- local preview

## 3. Privacy-first context preparation

Before AI:

- locally identify relevant entities
- redact/omit disallowed data
- summarize deterministic metadata
- include only minimum necessary content

AI gets bounded context, not full repository/investigation by default.

## 4. Content-addressed artifacts

Local hash:

- evidence file
- screenshot
- export
- report
- proof bundle

Digest becomes integrity metadata.

## 5. Offline mode

Without network, local WASM can still:

- load cached investigation
- replay history
- add local commands
- compare hypotheses
- compute blockers/capabilities
- create counterfactual branches
- semantic diff/time travel
- generate local report preview

AI-dependent functions degrade.

## 6. Deterministic parsers

For known artifact formats, local parser can extract candidate observations.

Examples:

- JSON
- CSV
- structured logs
- JUnit/test output
- Praxis result artifacts
- Aegis fault records

Extracted candidates remain proposals/findings until admitted.

## 7. Large-data prefilter

For large logs/artifacts:

- local filtering
- line/time range selection
- keyword/entity extraction
- chunk digesting

Only relevant chunks need AI analysis.

## 8. Evidence graph algorithms

Local graph operations:

- dependency closure
- cycles
- orphan nodes
- reachable evidence
- common-source groups
- shortest explanation paths

Results are derived diagnostics.

## 9. State-space engine

Run structural transition enumeration in WASM for:

- developer/factory view
- diagnostics
- contract validation where feasible

## 10. Competition demonstration

Turn off network and still show:

- investigation state
- time travel
- semantic diff
- counterfactual branch
- evidence graph
- local evidence hash
- pending commands

This makes offline behavior substantive rather than cosmetic.
