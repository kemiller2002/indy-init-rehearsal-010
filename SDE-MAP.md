# Repository Semantic Map

This initial routing map was established by Conditor from accepted governing inputs. It does not infer application semantics.

## Governing inputs

- Canonical execution contract: `kickoff/indy-init.kickoff.json`
- Accepted requirement artifacts:
- `kickoff`: `kickoff/indy-init.kickoff.json`
- `kickoff-schema`: `kickoff/kickoff-manifest.schema.json`
- `boundary`: `docs/HACKATHON-BOUNDARIES.md`
- `product`: `docs/PRODUCT-REQUIREMENTS.md`
- `release`: `docs/REQUIREMENTS.md`
- `semantics`: `docs/INVESTIGATION-SEMANTICS.md`
- `evolution`: `docs/SEMANTIC-EVOLUTION.md`
- `decisions`: `docs/DESIGN-DECISION-BOUNDARIES.md`
- `core`: `docs/CORE-DOMAIN-MODEL.md`
- `commands`: `docs/COMMAND-EVENT-CATALOG.md`
- `transitions`: `docs/STATE-TRANSITION-TABLES.md`
- `fsharp`: `docs/FSHARP-TYPE-SHAPES.md`
- `policy`: `docs/POLICY-ENGINE.md`
- `persistence`: `docs/PERSISTENCE-CONTRACT.md`
- `ai`: `docs/AI-ORCHESTRATION.md`
- `workspace`: `docs/INVESTIGATION-WORKSPACE.md`
- `screens`: `docs/SCREEN-CONTRACTS.md`
- `tests`: `docs/DOMAIN-TEST-MATRIX.md`
- `security`: `docs/SECURITY-THREAT-MODEL.md`
- `slices`: `docs/IMPLEMENTATION-SLICES.md`
- `competition`: `docs/COMPETITION-ALIGNMENT.md`
- `build-day`: `docs/BUILD-DAY-EXECUTION.md`
- `kickoff-packet`: `docs/KICKOFF-EXECUTION-PACKET.md`
- `fresh-agent-acceptance`: `docs/FRESH-AGENT-FACTORY-ACCEPTANCE.md`
- `factory-rehearsal`: `docs/FACTORY-REHEARSAL.md`
- `acceptance-scenarios`: `docs/INVESTIGATION-ACCEPTANCE-SCENARIOS.md`
- `open-questions`: `docs/OPEN-QUESTIONS.md`
- `semantic-compiler`: `docs/SEMANTIC-COMPILER.md`
- `proof-bundle`: `docs/ENGINEERING-PROOF-BUNDLE.md`
- `formal-validation`: `docs/FORMAL-VALIDATION.md`
- `proof-carrying-proposals`: `docs/PROOF-CARRYING-PROPOSALS.md`
- `tool-gateway`: `docs/TOOL-EXECUTION-GATEWAY.md`
- `planner`: `docs/INVESTIGATION-PLANNER.md`
- `bounded-autonomy`: `docs/BOUNDED-AUTONOMOUS-INVESTIGATION.md`
- `multi-agent`: `docs/MULTI-AGENT-INVESTIGATION.md`
- `cross-investigation`: `docs/CROSS-INVESTIGATION-INTELLIGENCE.md`
- `research-harness`: `docs/RESEARCH-HARNESS.md`
- `local-wasm-analytics`: `docs/LOCAL-WASM-ANALYTICS.md`
- `evidence-ingestion`: `docs/EVIDENCE-INGESTION-PIPELINE.md`
- `advanced-visualizations`: `docs/ADVANCED-VISUALIZATIONS.md`
- `domain-pack-contract`: `docs/DOMAIN-PACK-CONTRACT.md`
- `competition-ambition`: `docs/COMPETITION-AMBITION.md`
- `percepta-application`: `.percepta/application.json`
- `percepta-s01-investigation-start`: `.percepta/contracts/s01-investigation-start.json`
- `percepta-s02-investigation-workspace`: `.percepta/contracts/s02-investigation-workspace.json`
- `percepta-s03-hypothesis-battlefield`: `.percepta/contracts/s03-hypothesis-battlefield.json`
- `percepta-s04-hypothesis-detail`: `.percepta/contracts/s04-hypothesis-detail.json`
- `percepta-s05-evidence-capture`: `.percepta/contracts/s05-evidence-capture.json`
- `percepta-s06-evidence-detail`: `.percepta/contracts/s06-evidence-detail.json`
- `percepta-s07-evidence-dependency`: `.percepta/contracts/s07-evidence-dependency.json`
- `percepta-s08-epistemic-blocker-detail`: `.percepta/contracts/s08-epistemic-blocker-detail.json`
- `percepta-s09-action-experiment-designer`: `.percepta/contracts/s09-action-experiment-designer.json`
- `percepta-s10-finding-result-capture`: `.percepta/contracts/s10-finding-result-capture.json`
- `percepta-s11-cause-review`: `.percepta/contracts/s11-cause-review.json`
- `percepta-s12-corrective-action`: `.percepta/contracts/s12-corrective-action.json`
- `percepta-s13-verification`: `.percepta/contracts/s13-verification.json`
- `percepta-s14-history-time-travel`: `.percepta/contracts/s14-history-time-travel.json`
- `percepta-s15-semantic-diff`: `.percepta/contracts/s15-semantic-diff.json`
- `percepta-s16-counterfactual-sandbox`: `.percepta/contracts/s16-counterfactual-sandbox.json`
- `percepta-s17-multi-agent-panel`: `.percepta/contracts/s17-multi-agent-panel.json`
- `percepta-s18-research-tournament`: `.percepta/contracts/s18-research-tournament.json`
- `percepta-s19-replay-spectator`: `.percepta/contracts/s19-replay-spectator.json`
- `percepta-s20-factory-view`: `.percepta/contracts/s20-factory-view.json`
- `percepta-s21-requirements-proof`: `.percepta/contracts/s21-requirements-proof.json`
- `percepta-s22-report-preview`: `.percepta/contracts/s22-report-preview.json`
- `percepta-s23-persistence-conflict-recovery`: `.percepta/contracts/s23-persistence-conflict-recovery.json`
- `percepta-s24-settings-semantic-manifest`: `.percepta/contracts/s24-settings-semantic-manifest.json`
- `percepta-s25-demo-health`: `.percepta/contracts/s25-demo-health.json`

## Semantic areas

| Semantic area / feature | Purpose | Location | Manifest | Notes |
|---|---|---|---|---|
| Initial application mission | Establish the smallest semantic model required by the accepted contract | `src/engine/` | not established yet | Domain concepts, legal state, transitions, invariants, and guards are intentionally unknown until the governing inputs are interpreted. |

## Repository-wide composition

- Composition/root entry point: `src/kernel/bootstrap.ts`
- Shared contracts: see the governing inputs above.
- Architecture checks: installed Ordo/Praxis verification plus repository build/tests.
- Boundary checks: installed Limen and Aegis contracts where required.

## Areas without separate manifests

No feature manifest is generated merely to satisfy structure. Create one when the initial mission establishes a real semantic ownership boundary.
