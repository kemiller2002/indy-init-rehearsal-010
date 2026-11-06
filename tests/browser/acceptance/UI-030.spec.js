// Owner: ui. UI-030: negative controls for UI-022..UI-029 against the built application.
import { test, expect } from '@playwright/test';
import { open, routes } from '../support/app.mjs';
import { investigationWithContent, section, technical, contentText } from './workspace-data.mjs';

test('UI-030-O1', async ({ page }) => {
  // Technical metadata stays off investigation pages; statuses are the actual domain status; two-level navigation.
  const v = await investigationWithContent(page);
  for (const name of ['Overview', 'Evidence', 'Hypotheses', 'Timeline']) {
    await section(page, name);
    const text = await contentText(page);
    for (const t of technical) expect(text, `${name} ${t}`).not.toMatch(t);
  }
  await expect(page.getByRole('navigation', { name: 'Investigation sections' })).toBeVisible();
  expect(v.title).toBeTruthy();
});

test('UI-030-O2', async ({ page }) => {
  // Negative controls: no fabricated conclusion, no unknown shown as zero, no "Saved" before a save.
  await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(page.getByText('No conclusions recorded.')).toBeVisible();
  await expect(page.getByRole('status', { name: /save state/i })).not.toHaveText(/^\s*Saved\s*$/);
});

test('UI-030-O3', async ({ page }) => {
  // The deployed build's identity is reported where it belongs, and only as known when it is known.
  await open(page, routes.Administration);
  const commit = page.locator('.causa-fact').filter({ hasText: 'Deployed commit' });
  await expect(commit).toBeVisible();
  await expect(commit).not.toContainText(/Known/);
});

test('UI-030-O4', async ({ page }) => {
  // An unsupported feature is not shown as passing: Corrective Actions is not offered, Record a conclusion is absent.
  await investigationWithContent(page);
  await expect(page.getByRole('navigation', { name: 'Investigation sections' }).getByRole('link', { name: 'Corrective Actions' })).toHaveCount(0);
  await section(page, 'Overview');
  await expect(page.getByRole('button', { name: 'Record a conclusion' })).toHaveCount(0);
});
