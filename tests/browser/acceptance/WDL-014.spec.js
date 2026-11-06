// Owner: ui. WDL-014: the application really runs the identified workflow layout engine, at a pinned version.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { openSeededGraph } from './workspace-data.mjs';

test('WDL-014-O1', async ({ page }) => {
  // The pinned dependency (package.json and the lock) and, in the running app, the engine's element laid out the graph.
  const pkg = JSON.parse(readFileSync(new URL('../../../package.json', import.meta.url), 'utf8'));
  expect(pkg.dependencies['@echelon-foundry/forma-workflow']).toBe('1.2.0');
  const lock = JSON.parse(readFileSync(new URL('../../../package-lock.json', import.meta.url), 'utf8'));
  expect(lock.packages['node_modules/@echelon-foundry/forma-workflow'].version).toBe('1.2.0');
  const el = await openSeededGraph(page);
  expect(await page.evaluate(() => !!customElements.get('forma-workflow'))).toBe(true);
  await expect(el.locator('.ef-workflow-editor')).toBeVisible();
  const engineLoaded = await page.evaluate(() => performance.getEntriesByType('resource').some((r) => /forma-workflow\/engine\/_framework\//.test(r.name)));
  expect(engineLoaded).toBe(true);
});

test('WDL-014-O2', async () => {
  // No hand-coded diagram: Causa's sources and fragments draw no SVG and write no node coordinates.
  const files = ['src/Causa.App/Views/Graph.fs', 'web/screens/workspace-causal-analysis.html', 'web/screens/workspace-causal-analysis.css'];
  for (const f of files) {
    const text = readFileSync(new URL(`../../../${f}`, import.meta.url), 'utf8');
    expect(text, f).not.toMatch(/<svg|<path|viewBox|--ef-diagram-x|translate\(/);
  }
  const fragment = readFileSync(new URL('../../../web/screens/workspace-causal-analysis.html', import.meta.url), 'utf8');
  expect(fragment).toContain('<forma-workflow');
});
