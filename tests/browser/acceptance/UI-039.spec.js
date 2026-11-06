// Owner: ui. UI-039: UI-032..UI-038 share one typed relationship / selection / inspector / explanation contract.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { investigationWithContent, section } from './workspace-data.mjs';

const src = (p) => readFileSync(new URL(`../../../src/Causa.App/${p}`, import.meta.url), 'utf8');

test('UI-039-O1', async ({ page }) => {
  // One selection contract: every section selects with ?record=<id> and the same inspector answers.
  for (const f of ['Views/Workspace/Overview.fs', 'Views/Workspace/Hypotheses.fs', 'Views/Workspace/Evidence.fs', 'Views/Graph.fs']) expect(src(f), f).toContain('SectionBindings.recordCells');
  expect(src('Reporting.fs')).toContain('type SectionContext');
  const v = await investigationWithContent(page);
  for (const name of ['Overview', 'Hypotheses', 'Evidence']) {
    await section(page, name);
    for (const href of await page.getByRole('link', { name: 'Details' }).evaluateAll((as) => as.map((a) => a.getAttribute('href')))) expect(href, name).toMatch(/\?record=[0-9a-f-]{36}$/);
  }
  expect(v.h1).toBeTruthy();
});

test('UI-039-O2', async () => {
  // No duplicated domain logic: assessments come from the domain's Assessments module, capabilities from the kernel.
  const reporting = src('Reporting.fs');
  expect(reporting).toContain('Assessments.current');
  expect(reporting).toContain('Kernel.capabilities');
  for (const f of ['Views/Workspace/Evidence.fs', 'Views/Workspace/Hypotheses.fs', 'Views/Workspace/Links.fs']) expect(src(f), f).not.toMatch(/Decide\b|Evolve\b/);
});
