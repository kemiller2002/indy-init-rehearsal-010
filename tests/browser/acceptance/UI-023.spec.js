// Owner: ui. UI-023: technical validation is never an investigation-level "Verified".
import { test, expect } from '@playwright/test';
import { open, routes } from '../support/app.mjs';
import { investigationWithContent, section } from './workspace-data.mjs';

test('UI-023-O1', async ({ page }) => {
  // An investigation that loaded correctly is not labelled Verified anywhere on its pages.
  await investigationWithContent(page);
  for (const name of ['Overview', 'Causal Analysis', 'Evidence', 'Hypotheses', 'Timeline']) {
    await section(page, name);
    await expect(page.locator('main').getByText(/^Verified$/), name).toHaveCount(0);
  }
});

test('UI-023-O2', async ({ page }) => {
  // Data validation is reported on Administration; the investigation's badge is its domain lifecycle.
  await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(page.locator('[data-requirement-id="INV-018"] .ef-status-lozenge')).toHaveText(/^(draft|active|awaiting evidence|awaiting verification|resolved|closed|archived)$/);
  await open(page, routes.Administration);
  await expect(page.locator('.causa-fact').filter({ hasText: 'Data validation' })).toBeVisible();
});

test('UI-023-O3', async ({ page }) => {
  // Disjoint words: evidence verification says "unverified" while the investigation history was checked.
  const v = await investigationWithContent(page);
  await section(page, 'Evidence');
  await expect(page.getByRole('article').filter({ hasText: v.e1 }).first()).toContainText('Verification: unverified');
  await open(page, routes.Administration);
  await expect(page.locator('.causa-fact').filter({ hasText: 'Data validation' })).not.toContainText(/^Verified$/);
});

test('UI-023-O4', async ({ page }) => {
  // Regression: an investigation with a contradicting assessment still loads, and nothing calls it verified or resolved.
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(page.locator('dt:text-is("Evidence contradicting a hypothesis") + dd')).toHaveText('1');
  await expect(page.locator('main')).not.toContainText(/\bverified\b(?! )/i);
  await expect(page.getByText('No finding is supported or confirmed yet.')).toBeVisible();
  expect(v.r2).toBeTruthy();
});
