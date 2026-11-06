// Owner: ui. UI-004: commands come from the F# engine. Needs an investigation with content.
import { test, expect } from '@playwright/test';
import { open } from '../support/app.mjs';
import { investigationWithContent, section, field, button } from './workspace-data.mjs';

test('UI-004-O1', async ({ page }) => {
  // Only engine-authorized commands are rendered: "Record a conclusion" has no command, so it never appears, while
  // the commands the engine offers are present.
  await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(button(page, 'Record an observation')).toBeVisible();
  await expect(button(page, 'Record a conclusion')).toHaveCount(0);
  await expect(page.getByRole('list', { name: 'Actions available now' })).toContainText('Record a hypothesis');
});

test('UI-004-O2', async ({ page }) => {
  // A section without a verified investigation offers no command at all (no implied availability).
  await open(page, '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/hypotheses');
  await expect(page.getByText('This investigation is not open.')).toBeVisible();
  await expect(button(page, 'Record a hypothesis')).toHaveCount(0);
  await expect(field(page, 'Hypothesis statement')).toHaveCount(0);
});

test('UI-004-O3', async ({ page }) => {
  // Command inputs are labelled, length-limited form fields; the link kind is a choice with nothing preselected.
  await investigationWithContent(page);
  await section(page, 'Evidence');
  await expect(field(page, 'Evidence label')).toHaveAttribute('maxlength', '200');
  await expect(field(page, 'Evidence original content')).toHaveAttribute('maxlength', '10000');
  await expect(field(page, 'Evidence source URL')).toHaveAttribute('type', 'url');
  const kinds = page.getByRole('group', { name: 'Evidence relationship kind' }).getByRole('radio');
  await expect(kinds).toHaveCount(4);
  for (const r of await kinds.all()) await expect(r).not.toBeChecked();
});

test('UI-004-O4', async ({ page }) => {
  // A submission is not duplicated: recording once adds exactly one hypothesis card.
  const v = await investigationWithContent(page);
  await section(page, 'Hypotheses');
  const before = await page.getByRole('article').count();
  await field(page, 'Hypothesis statement').fill(v.h1 + '-once');
  await button(page, 'Record a hypothesis').click();
  await expect(page.getByRole('article')).toHaveCount(before + 1);
  await expect(page.getByRole('article').filter({ hasText: v.h1 + '-once' })).toHaveCount(1);
});

test('UI-004-O5', async ({ page }) => {
  // Keyboard invocation and 44px touch targets for the command buttons.
  const v = await investigationWithContent(page);
  await section(page, 'Hypotheses');
  await field(page, 'Hypothesis statement').fill(v.h2 + '-keyboard');
  await button(page, 'Record a hypothesis').focus();
  await page.keyboard.press('Enter');
  await expect(page.getByText(v.h2 + '-keyboard').first()).toBeVisible();
  const box = await button(page, 'Record a hypothesis').boundingBox();
  expect(box.height).toBeGreaterThanOrEqual(44);
});

test('UI-004-O6', async ({ page }) => {
  // No parallel policy engine: in a read-only catalog investigation (no write session) the record forms are absent
  // because the engine offers nothing there, and the page never shows a locally invented "allowed" state.
  await open(page, '#/investigation/workspaces%2Fdemo%2Fhistorical-v1/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/evidence');
  await expect(button(page, 'Record evidence')).toHaveCount(0);
  await expect(button(page, 'Link evidence to a hypothesis')).toHaveCount(0);
});
