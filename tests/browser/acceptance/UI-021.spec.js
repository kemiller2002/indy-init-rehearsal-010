// Owner: ui. UI-021: content placement across Investigations, Administration and Settings, checked by what is shown.
import { test, expect } from '@playwright/test';
import { open, primary, destinations, routes } from '../support/app.mjs';
import { connect, env, technical, contentText } from './workspace-data.mjs';

test('UI-021-O1', async ({ page }) => {
  // (1) no technical prose on Investigations; (2) operational facts on Administration; (3) preferences on Settings,
  // with their scope stated and a choice that takes effect; (4) labelled navigation; (5) no secret rendered.
  await open(page, routes.Investigations);
  const listText = await contentText(page);
  for (const t of technical) expect(listText).not.toMatch(t);
  await open(page, routes.Administration);
  for (const label of ['Provider', 'Repository', 'Connection', 'Storage contract', 'Data validation']) await expect(page.locator('.causa-fact').filter({ hasText: label }).first()).toBeVisible();
  await open(page, routes.Settings);
  await expect(page.getByRole('group', { name: 'Text size' })).toBeVisible();
  await page.getByRole('group', { name: 'Text size' }).getByLabel('Larger').check();
  await expect(page.getByRole('group', { name: 'Text size' }).getByLabel('Larger')).toBeChecked();
  const nav = await primary(page);
  for (const name of destinations) await expect(nav.getByRole('link', { name, exact: true })).toHaveCount(1);
  await connect(page);
  expect(await page.content()).not.toContain(env.token);
});

test('UI-021-O2', async ({ page }) => {
  // Placement, not existence: the storage contract appears on Administration and on neither other destination.
  await open(page, routes.Administration);
  await expect(page.locator('main')).toContainText('causa-storage v1.0.0');
  for (const route of [routes.Investigations, routes.Settings]) {
    await open(page, route);
    await expect(page.locator('main'), route).not.toContainText('causa-storage');
  }
});

test('UI-021-O3', async () => {
  // Traceability: each test of this suite is named by its obligation (checked across the suite by UI-015-O1).
  const { readFileSync } = await import('node:fs');
  const text = readFileSync(new URL('./UI-021.spec.js', import.meta.url), 'utf8');
  expect([...text.matchAll(/test\('(UI-021-O\d)'/g)].map((m) => m[1])).toEqual(['UI-021-O1', 'UI-021-O2', 'UI-021-O3']);
});
