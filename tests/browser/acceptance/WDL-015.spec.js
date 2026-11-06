// Owner: ui. WDL-015: every WDL requirement is traced to implementation and verification here or by an explicit
// exclusion; none is silently dropped.
import { test, expect } from '@playwright/test';
import { readFileSync, existsSync } from 'node:fs';

const slice = ['WDL-003', 'WDL-004', 'WDL-005', 'WDL-006', 'WDL-007', 'WDL-008', 'WDL-009', 'WDL-010', 'WDL-011', 'WDL-012', 'WDL-014', 'WDL-015'];

test('WDL-015-O1', async () => {
  // This slice's requirements each have a spec whose tests are named by both obligations.
  for (const id of slice) {
    const url = new URL(`./${id}.spec.js`, import.meta.url);
    expect(existsSync(url), id).toBe(true);
    const text = readFileSync(url, 'utf8');
    expect(text, id).toContain(`// Owner: ui. ${id}`);
    expect([...text.matchAll(/^test\('([^']+)'/gm)].map((m) => m[1]), id).toEqual([`${id}-O1`, `${id}-O2`]);
  }
});

/// The governing scope manifest (Indy-init docs/rehearsal-010-readiness/scope-manifest.json), from the governed Indy-init
/// checkout: INDY_INIT_ROOT, else the verification gate's $RUNNER_TEMP/indy-init, else a sibling ../Indy-init. It must be
/// the scope this repository pinned: its content hash (the requirements compiler's rule: SHA-256 of the manifest without
/// scopeSha256 and source, two-space JSON, ASCII-escaped, trailing newline) equals .echelon/scope-checkpoint.json.
const governedScope = async () => {
  const { createHash } = await import('node:crypto');
  const candidates = [process.env.INDY_INIT_ROOT, process.env.RUNNER_TEMP && `${process.env.RUNNER_TEMP}/indy-init`, new URL('../../../../Indy-init', import.meta.url).pathname].filter(Boolean);
  const found = candidates.map((root) => `${root}/docs/rehearsal-010-readiness/scope-manifest.json`).find((path) => existsSync(path));
  expect(found, `the governing scope manifest is not available (set INDY_INIT_ROOT); looked in ${candidates.join(', ')}`).toBeTruthy();
  const manifest = JSON.parse(readFileSync(found, 'utf8'));
  const { scopeSha256, source, ...content } = manifest;
  const canonical = JSON.stringify(content, null, 2).replace(/[\u007f-￿]/g, (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')) + '\n';
  const pinned = JSON.parse(readFileSync(new URL('../../../.echelon/scope-checkpoint.json', import.meta.url), 'utf8')).scopeSha256;
  expect(createHash('sha256').update(canonical, 'utf8').digest('hex'), 'the scope manifest is the pinned scope').toBe(pinned);
  expect(scopeSha256).toBe(pinned);
  return manifest;
};

test('WDL-015-O2', async () => {
  // Criterion: no WDL obligation is silently omitted during derivation. Every WDL-001..WDL-015 is accounted for in the
  // pinned scope and the compiled plan: a requirement with obligations in the plan has each one bound to a test, and
  // the manifest's own obligations for it are all present (none dropped); a requirement without plan obligations must
  // be held in scope at gate level by the approved rule (gateLevelScope, ODR-V4-10 option A: "not excluded") until its
  // obligations are derived (WDL-013: owner decision, successor package v4.1.6). No WDL requirement may be excluded.
  const manifest = await governedScope();
  const plan = JSON.parse(readFileSync(new URL('../../../.echelon/obligations.json', import.meta.url), 'utf8'));
  expect(manifest.gateLevelScope.rule).toMatch(/stay in scope .* They are not excluded\./);
  for (let n = 1; n <= 15; n++) {
    const id = `WDL-${String(n).padStart(3, '0')}`;
    expect(manifest.exclusions.requirements.map((r) => r.id ?? r.requirement ?? r), id).not.toContain(id);
    const derived = plan.obligations.filter((o) => o.requirement === id).map((o) => o.id);
    const scoped = manifest.requirements.find((r) => r.id === id);
    for (const o of scoped ? scoped.obligations.map((x) => x.id) : []) expect(derived, `${o} is in the scope but not in the compiled plan`).toContain(o);
    if (derived.length > 0) {
      for (const o of derived) expect(plan.tests.some((t) => (t.obligations || []).includes(o)), `${o} has no bound test`).toBe(true);
    } else {
      expect(manifest.gateLevelScope.requirements, `${id} has no obligations and is not held at gate level`).toContain(id);
    }
  }
  // This slice's obligations are each a test above.
  for (const id of slice) for (const o of plan.obligations.filter((x) => x.requirement === id).map((x) => x.id)) {
    expect(readFileSync(new URL(`./${id}.spec.js`, import.meta.url), 'utf8'), o).toContain(`test('${o}'`);
  }
});
