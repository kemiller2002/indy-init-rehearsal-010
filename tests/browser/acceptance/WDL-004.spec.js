// Owner: ui. WDL-004: Causa owns graph semantics, the workflow engine owns placement and routing, Forma owns the
// components; each is testable on its own.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { renderGraph } from './workspace-data.mjs';

test('WDL-004-O1', async ({ page }) => {
  // Causa writes meaning and no geometry; the engine writes every position; the nodes are Forma diagram components.
  const { el, workflow } = await renderGraph(page, 'four-roles');
  const text = JSON.stringify(workflow);
  expect(text).not.toMatch(/"x"|"y"|"width"|"height"|"waypoints"/);
  expect(workflow.layout).toEqual({ direction: 'right', mode: 'auto' });
  const styles = await el.locator('.ef-diagram-node').evaluateAll((ns) => ns.map((n) => n.getAttribute('style')));
  expect(styles.length).toBe(5);
  for (const s of styles) expect(s).toMatch(/--ef-diagram-x: [\d.]+rem; --ef-diagram-y: [\d.]+rem/);
});

test('WDL-004-O2', async () => {
  // The three responsibilities are tested apart: the projection in .NET (CausalGraphTests), placement here against
  // the engine's output, rendering/accessibility in WDL-007; the adapter has no rendering code and no layout code.
  const adapter = readFileSync(new URL('../../../src/Causa.App/Views/Graph.fs', import.meta.url), 'utf8');
  expect(adapter).toContain('module CausalGraph');
  expect(adapter).not.toMatch(/<svg|viewBox|getBoundingClientRect/);
  const tests = readFileSync(new URL('../../Causa.Contract.Tests/Report/Producer.fs', import.meta.url), 'utf8');
  expect(tests).toContain('module CausalGraphTests');
});
