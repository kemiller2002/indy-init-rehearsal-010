// Owner: ui. UI-038: conclusions are explained from typed records. Recording a conclusion has no command in the frozen
// contract (INV-032, owner decision pending), so a conclusion reaches the app only from a stored history.
import { test, expect } from '@playwright/test';
import { investigationWithContent, section } from './workspace-data.mjs';

test('UI-038-O1', async ({ page }) => {
  // Each shown conclusion carries its explanation; where none exists the page says so instead of inventing one.
  await investigationWithContent(page);
  await section(page, 'Overview');
  const conclusions = page.getByRole('list', { name: 'Conclusions' }).getByRole('article');
  const count = await conclusions.count();
  if (count === 0) await expect(page.getByText('No conclusions recorded.')).toBeVisible();
  for (const c of await conclusions.all()) await expect(c.locator('[data-requirement-id="UI-038"]')).toContainText(/supporting evidence|No causal assertion is linked/);
});

test('UI-038-O2', async ({ page }) => {
  // Explanations name only recorded evidence; unlinked hypotheses say "none recorded" rather than a guess.
  const v = await investigationWithContent(page);
  await section(page, 'Hypotheses');
  await page.getByRole('article').filter({ hasText: v.h2 }).getByRole('link', { name: 'Details' }).click();
  await expect(page.getByRole('complementary')).toContainText('No linked records.');
  await expect(page.getByRole('complementary')).toContainText('Explanation: not recorded');
});
