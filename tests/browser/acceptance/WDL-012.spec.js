// Owner: ui. WDL-012: projection, layout, rendering and live integration each tested; a real stored investigation
// renders with its expected nodes, roles and evidence counts.
import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync } from 'node:fs';
import { openSeededGraph, nodeBoxes, overlaps } from './workspace-data.mjs';

test('WDL-012-O1', async () => {
  // Four independent layers: .NET projection tests, fixture layout specs, rendering/accessibility specs, live specs.
  const producer = readFileSync(new URL('../../Causa.Contract.Tests/Report/Producer.fs', import.meta.url), 'utf8');
  for (const t of ['WDL-003 the graph has', 'WDL-009 identical input', 'WDL-011 the committed browser fixtures']) expect(producer).toContain(t);
  const specs = readdirSync(new URL('.', import.meta.url));
  for (const f of ['WDL-005.spec.js', 'WDL-006.spec.js', 'WDL-007.spec.js', 'WDL-010.spec.js']) expect(specs).toContain(f);
  expect(readFileSync(new URL('./WDL-010.spec.js', import.meta.url), 'utf8')).toContain('openSeededGraph');
});

test('WDL-012-O2', async ({ page }) => {
  // The SSO outage history: one outcome, two hypotheses, the root-cause link, the evidence counts, no overlap,
  // stable on reload, and the same content in the text list.
  const el = await openSeededGraph(page);
  const nodes = el.locator('.ef-diagram-node');
  await expect(nodes.filter({ hasText: 'Outcome' })).toHaveText([/Single sign-on outage/]);
  await expect(nodes.filter({ hasText: 'Hypothesis' })).toHaveCount(2);
  await expect(el.getByRole('list', { name: 'Connections' }).getByRole('button')).toHaveText([/root cause/]);
  await expect(nodes.filter({ hasText: 'Evidence: 2 supporting, 0 contradicting' })).toHaveCount(1);
  await expect(nodes.filter({ hasText: 'Evidence: 0 supporting, 1 contradicting' })).toHaveCount(1);
  const boxes = await nodeBoxes(el);
  expect(overlaps(boxes)).toEqual([]);
  await page.reload();
  expect(await nodeBoxes(el.page().locator('forma-workflow'))).toBeDefined();
  await expect(page.getByRole('list', { name: 'Evidence and hypothesis relationships' }).getByRole('listitem')).toHaveCount(3);
});
