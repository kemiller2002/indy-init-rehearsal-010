**Governed verdict: NONE. No governed acceptance was obtained. Every merge is implementer-verified under owner rule B, not governed-accepted. Rehearsal 010 is NOT complete.**

# Rehearsal 010 — final report

| | |
|---|---|
| Repository | kemiller2002/indy-init-rehearsal-010 |
| Main at report | 62c1a90 (plus this report's merge) |
| Requirements | Indy-init `requirements-compiled/v4.1.5`; approved scope 26e6ff3c… (205 requirements, 890 obligations, 37 work items) |
| T0 / deadline | 2026-11-06T16:40:13Z / 2026-11-06T23:40:13Z (`evidence/t0/t0.json`, 420-minute window, not reset) |
| Stop reason | Deadline. No stop-as-complete was requested: the work is not complete. |

## 1. Acceptance ledger

**Not produced.** No release-mode Verification gate verdict was obtained for any merge, so there is no completion ledger to report.

The governed gate could not run to a verdict in this rehearsal. The causes were:
- the 40-minute cap combined with the governance record that can be written only once;
- dependency order;
- Playwright installs on the runner timing out.

These are recorded in §10. I do not substitute a self-computed ledger for the governed one.

## 2. Mandatory requirements not integrated, with reasons

Exact per-requirement coverage was not computed against the ledger (see §1). The following is stated at work-item level from the merge record.

**Not merged, blocked:**
- **SL-T23, SL-T24, SL-WI-GEN-WDL** (causal workflow diagram, WDL-001..015).
  - The merge result builds, and all other non-live tests pass.
  - VER-001-O1/O2 correctly refuse `@echelon-foundry/forma-workflow@1.2.0`, because it is outside the locked component set (`.conditor/lock.json`).
  - Conditor did not know the component until conditor#132. Conditor v0.10.21 (released 22:2xZ) does, but adding it to this repository's lock needs an owner re-pin of the R10 toolchain lock. That was not done in the window.
- **SL-T22, T29–T36** and the remaining DAG items: not started, or not completed, within the window.
- **setup/praxis-checkpoints**: held. It failed Praxis validation after a merge of mine (§10).
- **setup/test-deploy-key**: held pending owner decision ODR-R10-36.

**Merged but with known failing obligations (browser suite, Chromium 20 failing / WebKit 19):**
- **Accessibility:** contrast 4.0:1 and 4.39:1 (needs 4.5:1); duplicate assertive live region; badge not visible at 200% text.
- **Save flow:** the record header blanks during the re-read after a save.
- **UI wording:** copy and selector mismatches in UI-022-O6, UI-025-O1/O2, UI-026-O2, UI-032-O2, UI-038-O2, UI-005-O3 and UI-001-O6.
- **Unchanged from before:** UI-015-O1/O4, UI-017-O4, UI-021-O2 and UI-030-O3.
- **Timing-sensitive:** PERSIST-031-O19, Chromium only.

**Live-provider obligations:** BLOCKED in this session. CAUSA_GITHUB_TOKEN is not set here, so they fail as BLOCKED and are never skipped.

**Contract gaps (resolved for the competition, not for R10):**
- J2-03/04/06/07 need RecordConclusion and the storage 1.1.0 events. These were delivered as v4.1.6 and causa-storage 1.1.0, with owner decisions recorded in APR-V416-OWNER-1 and the tag `causa-storage-v1.1.0`. R10 builds against v4.1.5, so they remain unimplemented here.
- WDL-013 had no obligations under v4.1.5. Fixed for v4.1.6 by ODR-V416-WDL013 and the selector fix (#176).

## 3. Timestamps

| Time (UTC) | Event |
|---|---|
| 16:40 | T0; bootstrap pushed |
| 16:50–16:59 | Scope checkpoint and initialization checkpoint merged |
| 17:03 | `authorize-implementation` refused structurally (r9 defect); owner directed the fix |
| 17:45 | Progress events recorded |
| 18:48–19:40 | First slices T10–T20 merged |
| 20:09 | T17, the end-to-end app, merged |
| 20:14–21:00 | WI-GEN-UI, VER, RECORDS, PF; T21, T25 |
| 22:33 | Browser suites on the stateful fake, plus the credential-refused save fix (#22) |
| 23:40 | Deadline |

Every merge time is in the PR history (#1–#22).

## 4. Provenance

**Authorship.** All implementation was done by Claude Code agents in one cloud session, coordinated by one session acting as integrator. The Praxis actor was `claude`.
- Some early lane events used lane names as the actor. That was my briefing defect (§10).
- No owner record was written by an agent. Every owner decision was recorded by the owner with `owner-freeze.fsx`.

**Owner decisions taken during the window (owner rule B):**
- **Merge criteria.** A merge needs all of:
  - CI green on the code commit, or a stated exception;
  - a local build of the exact merge result;
  - all non-live tests passing;
  - browser specs passing for UI slices;
  - check-sequencing PASS.

  A governed verdict is recorded where one exists, but is not required.
- **Labelling.** Every merge commit says "implementer-verified, not governed-accepted".

## 5. Toolchain

- .NET SDK 10.0.402, pinned by `global.json`.
- F# with FSharp.Core 10.1.402, central package. Added in SL-T11 because, without it, no test assembly was discovered.
- Limen 0.9.1, Forma 0.13.0 and Folio 0.4.1.
- Praxis 3.25.0 and Ordo 1.20.0.
- Conditor 0.10.20, the pinned version.
- Playwright 1.63.0 (Chromium and WebKit) and Node 22.

## 6. Launch procedure as executed

1. Preflight against Indy-init STARTUP-INSTRUCTIONS.
2. Conditor-first bootstrap. `conditor repo create` refused the pre-existing repository, so the bootstrap was pushed directly.
3. T0 record.
4. Scope checkpoint and Praxis registration.
5. `authorize-implementation`: refused. Repaired under owner direction (Indy-init PR #174), then PASS at 33fd372.
6. Lanes in dependency order.

Deviations, each recorded:
- **Branch protection.** It could not be set from the session, which is an owner action.
- **Competition-repo secrets.** They were added by the owner during the window.
- **Merge rule.** Rule B replaced the governed gate after the gate deadlock.

## 7. Journeys

J1-01..06 and J3-01..10 pass locally, implementer-run, in Chromium and WebKit, against the build of 1e642ea and the GitHub double.
- J3-06/07 inject failures at the GraphQL `createCommitOnBranch` boundary the app actually uses. Each injection was proven to fire, and the negative control fails without the fix.

J2-03/04/06/07 cannot pass under v4.1.5 (§2).

No journey ran in the governed gate.

## 8. Coverage

Not computed against the governed ledger (§1). Test inventory at main, local Release run of the merge result:

| Suite | Passed |
|---|---|
| Domain | 9/9 |
| App | 14/14 |
| Contract | 50/50 |
| Invariants | 10/10 |
| Persistence | 102/102 |
| Acceptance (deterministic) | 93/93 |
| **Total non-live** | **278/278** |

Browser acceptance and invariants, 173 tests:
- Chromium: 153 passed, 20 failed.
- WebKit: 154 passed, 19 failed.

Live suites: BLOCKED (no credential in session).

## 9. CI status

- **build-and-test:** cancelled by its 20-minute job limit on main and on every PR since the browser suites became real. The browser suites alone take about 29 minutes.
- **Governed Verification gate:**
  - It failed before any test ran: the Playwright install timed out at 10 minutes.
  - It refused non-`slice/SL-*` branch names (#22).
  - No governed verdict was produced.
- **Other workflows:** echelon-foundations, validate and the Praxis validation passed throughout.

## 10. Failures and corrective actions

**Process and tooling defects found and fixed during the window:**

| Defect | Fix |
|---|---|
| r9 `authorize-implementation` refused a valid plan (55 causes) | Indy-init #174 |
| FSharp.Core missing under central package management, so 0 tests ran | SL-T11 |
| Gate deadlock | Owner rule B |
| Design contract refused literal brand colours in `dist/forma/brands` | Build copies only `forma/all.css` |
| Vendored demonstration records violated EXT-DATA-011-O1 | Removed; live fetch tests instead |
| AppDriver module collision in a merge result | Renamed to EngineDriver |
| Selector compiler ignored `doc:`/`prefix:`/`origin:`, so WDL-013 had no obligations | Indy-init #176 |
| Offline browser specs required real credentials (PF-005/PF-011), and the harness double did not serve the app's refs and GraphQL calls | Repository's stateful fake served over HTTP, #22 |
| Credential-refused save never reached CredentialRefused, so Reconnect led nowhere (INV-25) | #22, with a test |

**My own defects:**
- **Lane briefs.** They told lanes to use their lane name as the Praxis actor; it must be `claude`.
- **Stash.** A `git stash`/`pop` of mine lost the index, so the scope-checkpoint commit lacked `.ros`. I rebuilt my own unmerged branch.
- **Sync script.** It pushed despite a failed Praxis validation (setup/praxis-checkpoints). The script now refuses to push on failure, and the branch is held.
- **Double writer.** Two lanes were briefed to edit InvestigationList.fs.
- **Merge during a gate run.** I merged evidence while a gate run was in progress, which made STALE_TARGET.
- **Rejected lane fallback.** I rejected a lane's fabricated evidence fallback (INV-028 violation) before it merged.

**Lessons for the competition (actioned tonight in Indy-init, Conditor and causa-data):**
- v4.1.6 and causa-storage 1.1.0 (RecordConclusion, revision events) are recorded and tagged.
- Process fixes #174 are merged.
- Conditor v0.10.21 carries forma-workflow in the preset and the deploy template.
- Startup procedure for the pre-created repository (#182).
- Browser cache (#151).
- The competing build must raise `build-and-test`'s time limit or split the browser projects.

## 11. Time breakdown (approximate)

| From–to (UTC) | Minutes | Activity |
|---|---|---|
| 16:40–17:05 | 25 | Bootstrap, checkpoints, authorization refusal |
| 17:05–18:45 | 100 | r9 repair, secrets, gate attempts, foundation slices |
| 18:45–21:00 | 135 | Slice merges, 20 PRs |
| 21:00–22:35 | 95 | Test-harness root cause and fix; competition preparation in parallel |
| 22:35–23:40 | 65 | Competition preparation (owner decisions, releases); this report |

## 12. Unfinished or blocked: summary

The work is not complete. Blocked or held items are listed in §2, and the governed verdict is absent (§1). No unfinished item is reported as complete.

## 13. Evidence index

- `evidence/t0/` holds the T0 record, the preflights, the authorization refusal and its blocker record.
- `evidence/progress/events.json` holds the progress events.
- PRs #1–#22 carry the merge comments, which state every rule-B exception.
- Local logs are in the session scratchpad and are not committed.
