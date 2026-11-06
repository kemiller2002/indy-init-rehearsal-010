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

test('WDL-015-O2', async () => {
  // Nothing is silently omitted: every WDL obligation the plan derives (.echelon/obligations.json) has a bound test
  // identity, and every one of this slice's is one of the tests above.
  const plan = JSON.parse(readFileSync(new URL('../../../.echelon/obligations.json', import.meta.url), 'utf8'));
  const obligations = plan.obligations.filter((o) => o.requirement.startsWith('WDL-')).map((o) => o.id);
  const derived = new Set(obligations.map((o) => o.replace(/-O\d+$/, '')));
  // A requirement without obligations needs a recorded, approved scope exclusion (the scope checkpoint); WDL-013
  // (the P1 comparative experiment) has neither today, so this fails until one is recorded.
  const scope = readFileSync(new URL('../../../.echelon/scope-checkpoint.json', import.meta.url), 'utf8');
  for (let n = 1; n <= 15; n++) {
    const id = `WDL-${String(n).padStart(3, '0')}`;
    expect(derived.has(id) || scope.includes(id), `${id} is neither derived nor an approved exclusion`).toBe(true);
  }
  for (const o of obligations) expect(plan.tests.some((t) => (t.obligations || []).includes(o)), o).toBe(true);
  for (const id of slice) for (const o of obligations.filter((x) => x.startsWith(id + '-'))) {
    const text = readFileSync(new URL(`./${id}.spec.js`, import.meta.url), 'utf8');
    expect(text, o).toContain(`test('${o}'`);
  }
});
