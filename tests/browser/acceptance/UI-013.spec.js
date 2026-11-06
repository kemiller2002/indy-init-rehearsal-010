// Owner: ui. UI-013: one investigation, one version, across every view.
import { test, expect } from '@playwright/test';
import { open } from '../support/app.mjs';
import { investigationWithContent, section, field, button } from './workspace-data.mjs';

const names = ['Overview', 'Causal Analysis', 'Evidence', 'Hypotheses', 'Timeline'];

test('UI-013-O1', async ({ page }) => {
  // Every section names the same investigation id and the same version.
  const v = await investigationWithContent(page);
  const id = new URL(v.url).hash.split('/')[3];
  const versions = new Set();
  for (const name of names) {
    await section(page, name);
    expect(page.url()).toContain(id);
    versions.add(await page.locator('.causa-analysis__version').first().textContent());
  }
  expect(versions.size).toBe(1);
  expect([...versions][0]).toMatch(/^Version \d+$/);
});

test('UI-013-O2', async ({ page }) => {
  // A successful command refreshes every dependent view from the new version.
  const v = await investigationWithContent(page);
  await section(page, 'Hypotheses');
  const before = await page.locator('.causa-analysis__version').textContent();
  await field(page, 'Hypothesis statement').fill(v.h2 + '-fresh');
  await button(page, 'Record a hypothesis').click();
  await expect(page.locator('.causa-analysis__version')).not.toHaveText(before);
  const after = await page.locator('.causa-analysis__version').textContent();
  for (const name of names) { await section(page, name); await expect(page.locator('.causa-analysis__version').first()).toHaveText(after); }
  await section(page, 'Timeline');
  await expect(page.getByText(`Hypothesis recorded: ${v.h2}-fresh`)).toBeVisible();
});

test('UI-013-O3', async ({ page }) => {
  // No view mixes versions: the Overview counts match the Hypotheses and Evidence lists of the same version.
  await investigationWithContent(page);
  await section(page, 'Overview');
  const hyp = await page.locator('dt:text-is("Hypotheses") + dd').textContent();
  const ev = await page.locator('dt:text-is("Evidence items") + dd').textContent();
  await section(page, 'Hypotheses');
  await expect(page.getByRole('list', { name: 'Hypotheses', exact: true }).getByRole('article')).toHaveCount(Number(hyp));
  await section(page, 'Evidence');
  await expect(page.getByRole('list', { name: 'Evidence', exact: true }).getByRole('article')).toHaveCount(Number(ev));
});

test('UI-013-O4', async ({ page }) => {
  // Deep links resolve or fail usefully: a malformed one is "page not found"; an unopened one says how to recover.
  await open(page, '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/not-a-guid/overview');
  await expect(page.getByText(/There is no page at/)).toBeVisible();
  await open(page, '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/timeline');
  await expect(page.getByText('Go to Investigations and open it from the list.')).toBeVisible();
  await open(page, '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/evidence?record=unknown');
  await expect(page.getByText('This investigation is not open.')).toBeVisible();
});
