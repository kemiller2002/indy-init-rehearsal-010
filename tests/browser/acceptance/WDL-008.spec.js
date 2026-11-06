// Owner: ui. WDL-008: Forma components and tokens; colour only repeats a domain state.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { renderGraph, graphFixtures, graphFixture } from './workspace-data.mjs';

test('WDL-008-O1', async ({ page }) => {
  // Nodes are Forma diagram components; any authored colour is a Forma status token, never a literal.
  const { el } = await renderGraph(page, 'four-roles');
  await expect(el.locator('figure.ef-diagram')).toHaveCount(1);
  await expect(el.locator('article.ef-diagram-node')).toHaveCount(5);
  for (const name of graphFixtures) {
    const text = await graphFixture(name);
    expect(text, name).not.toMatch(/#[0-9a-fA-F]{6}/);
    for (const m of text.match(/"token:[a-z-]+"/g) || []) expect(['"token:status-success"', '"token:status-danger"'], name).toContain(m);
  }
});

test('WDL-008-O2', async () => {
  // The adapter colours only a confirmed cause and a falsified hypothesis, from the domain state; no fixture has
  // either, so none carries a colour, and placement never decides one.
  const adapter = readFileSync(new URL('../../../src/Causa.App/Views/Graph.fs', import.meta.url), 'utf8');
  expect(adapter).toContain('| OutcomeNode, "confirmed" ->');
  expect(adapter).toContain('| HypothesisNode, "falsified" ->');
  for (const name of graphFixtures) {
    const wf = JSON.parse(await graphFixture(name));
    for (const n of wf.nodes) expect(n.color, `${name} ${n.id}`).toBeUndefined();
    expect(wf.nodes.every((n) => ['proposed'].includes(n.status.label)), name).toBe(true);
  }
});
