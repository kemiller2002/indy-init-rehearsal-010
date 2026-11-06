# Rehearsal 010: implementation authorization blocked

- T0 2026-11-06T16:40:13Z; deadline 2026-11-06T23:40:13Z (evidence/t0/t0.json). Not reset.
- Bootstrap 314cc8c (Conditor 0.10.20, v4.1.5, SRC 89f5840). First green CI on PR #1; merged as 33fd372
  with the scope checkpoint (scopeSha256 26e6ff3c = lock).
- `r9.fsx authorize-implementation` (STARTUP §3 step 4) REFUSED, 55 causes
  (evidence/t0/authorize-implementation.refused.log). No application code has been committed.

## The two blockers (governed tooling; not repairable by the builder)

1. **Step 3, 37 causes.** r9.fsx:885 matches Praxis work items by id. STARTUP §3 step 2 registers them with
   `./praxis add "[ID] title"` (Praxis assigns WI-0001..WI-0037) or `./praxis work begin --id ID` (no such Praxis
   3.25.0 command; `work start` needs an existing item). Praxis 3.25.0 refuses `T10`..`T36` as ids
   (`ERROR invalid work-item ID 'T11'`), so 27 of the 37 approved items can never be "in Praxis" by id.
   `requirements-compiler scope preflight` accepts the `[ID] title` form, so the two gates disagree.
2. **Step 4, 18 causes.** r9.fsx:911-912 refuse a work item with no requirement or no obligation. The generated,
   approved work plan has 9 such items by design: T14 T17 T20 T23 T26 T27 T28 T32 T35.

`tools/release-gate-repair/tests.fsx` exercises authorize-implementation only with a synthetic `WI-CONTENT-1` plan.

## Decision

`requirements-compiler stop-decision` (reason hard-blocker, both blockers evidenced): STOP_PERMITTED
(evidence/progress/stop-decision.json). The run resumes against the original deadline once the owner acts
(STARTUP §7, `requirements-compiler resume`).

## Owner actions that unblock it (any one)

- Repair r9.fsx authorize-implementation in Indy-init (resolve `[ID] title` Praxis items; accept approved items
  with no links), merge it, and re-pin the lock's governing commit (ODR-RG-04) so `govern` and LP-00 read it; or
- record an owner decision authorizing implementation with these two refusals stated, naming the refusal log.
