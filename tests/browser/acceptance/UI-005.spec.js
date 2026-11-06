// Owner: ui. UI-005: evidence with its provenance, original content, links and safe rendering.
import { test, expect } from '@playwright/test';
import { investigationWithContent, section, field, button } from './workspace-data.mjs';

const card = (page, text) => page.getByRole('article').filter({ hasText: text }).filter({ hasText: 'Category' }).first();

test('UI-005-O1', async ({ page }) => {
  // Source, record time and actor, original content, verification and links to the hypotheses it bears on.
  const v = await investigationWithContent(page);
  await section(page, 'Evidence');
  const c = card(page, v.e1);
  await expect(c).toContainText(v.e1);
  await expect(c).toContainText(`Source: ${v.s1}`);
  await expect(c).toContainText(/Recorded by .+ at \d{4}-\d\d-\d\dT/);
  await expect(c).toContainText('Verification: unverified');
  await expect(c).toContainText(`supports: ${v.h1}`);
});

test('UI-005-O2', async ({ page }) => {
  // Reassessing the evidence never changes its original content.
  const v = await investigationWithContent(page);
  await section(page, 'Evidence');
  await page.getByRole('combobox', { name: /evidence/i }).first().selectOption({ label: v.e1 });
  await page.getByRole('combobox', { name: /hypothes/i }).first().selectOption({ label: v.h1 });
  await page.getByRole('group', { name: 'Evidence relationship kind' }).getByLabel('contradicts', { exact: true }).check();
  await field(page, 'Evidence relationship rationale').fill('reconsidered');
  await button(page, 'Reassess').click();
  await expect(card(page, v.e1)).toContainText(`contradicts: ${v.h1}`);
  await expect(card(page, v.e1).locator('.causa-record__text')).toHaveText(v.e1);
});

test('UI-005-O3', async ({ page }) => {
  // Untrusted text is text, not markup; only an http(s) source becomes a link; missing capture time is labelled.
  const v = await investigationWithContent(page);
  await section(page, 'Evidence');
  const hostile = '<img src=x onerror="window.__pwned=1">' + v.e2;
  await field(page, 'Evidence label').fill(hostile);
  await field(page, 'Evidence source reference').fill('javascript:alert(1)');
  await field(page, 'Evidence original content').fill(hostile);
  await button(page, 'Record evidence').click();
  const c = card(page, v.e2 + '').filter({ hasText: '<img' });
  await expect(c).toContainText('<img src=x');
  expect(await page.evaluate(() => window.__pwned)).toBeUndefined();
  await expect(c.getByRole('link', { name: /javascript/ })).toHaveCount(0);
  await expect(card(page, v.e1).getByRole('link', { name: v.s1 })).toHaveAttribute('href', v.s1);
  await expect(c).toContainText('Capture time not recorded');
});

test('UI-005-O4', async ({ page }) => {
  // Evidence is reachable from the causal analysis, the timeline and the hypothesis view.
  const v = await investigationWithContent(page);
  await section(page, 'Causal Analysis');
  await page.getByRole('link', { name: v.e1 }).first().click();
  await expect(page.getByRole('complementary').filter({ hasText: v.e1 })).toBeVisible();
  await section(page, 'Timeline');
  await page.getByRole('listitem').filter({ hasText: `Evidence recorded: ${v.e1}` }).getByRole('link', { name: 'Open the evidence' }).click();
  await expect(page.getByRole('complementary').filter({ hasText: v.e1 })).toBeVisible();
  await section(page, 'Hypotheses');
  await page.getByRole('article').filter({ hasText: v.h1 }).getByRole('link', { name: 'Details' }).click();
  await page.getByRole('complementary').getByRole('link', { name: `supports: ${v.e1}` }).click();
  expect(page.url()).toMatch(/\/evidence\?record=/);
});
