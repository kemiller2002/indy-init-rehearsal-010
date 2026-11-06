// Owner: ui. UI-032: next steps derived from the domain, each linked to its record.
import { test, expect } from '@playwright/test';
import { investigationWithContent, section } from './workspace-data.mjs';

test('UI-032-O1', async ({ page }) => {
  // The second hypothesis has no assessed evidence: that is the derived next step.
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  const steps = page.getByRole('list', { name: 'Next steps' });
  await expect(steps).toContainText(`No evidence has been assessed against the hypothesis: ${v.h2}`);
  await expect(steps).not.toContainText(v.h1);
});

test('UI-032-O2', async ({ page }) => {
  // Each step links to its record, and the step goes away when the work is done (never invented, never stale).
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  await page.getByRole('link', { name: `No evidence has been assessed against the hypothesis: ${v.h2}` }).click();
  expect(page.url()).toMatch(/\/hypotheses\?record=/);
  await expect(page.getByRole('complementary').filter({ hasText: v.h2 })).toBeVisible();
  await section(page, 'Evidence');
  await page.getByRole('combobox', { name: /evidence/i }).first().selectOption({ label: v.e2 });
  await page.getByRole('combobox', { name: /hypothes/i }).first().selectOption({ label: v.h2 });
  await page.getByRole('group', { name: 'Evidence relationship kind' }).getByLabel('supports', { exact: true }).check();
  await page.getByLabel('Evidence relationship rationale', { exact: true }).fill('assessed now');
  await page.getByRole('button', { name: 'Link evidence to a hypothesis', exact: true }).click();
  await section(page, 'Overview');
  await expect(page.getByText('The recorded content raises no next step.')).toBeVisible();
});
