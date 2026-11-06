// Owner: ui. UI-025: the Overview answers from the domain's projection only.
import { test, expect } from '@playwright/test';
import { investigationWithContent, section } from './workspace-data.mjs';

const dd = (page, term) => page.locator(`dt:text-is("${term}") + dd`);

test('UI-025-O1', async ({ page }) => {
  // What happened, findings, evidence for and against, unresolved questions and next authorized actions.
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(dd(page, 'Original problem statement')).toHaveText(v.problem);
  await expect(page.getByRole('heading', { name: 'Current findings' })).toBeVisible();
  await expect(dd(page, 'Evidence supporting a hypothesis')).toHaveText('1');
  await expect(dd(page, 'Evidence contradicting a hypothesis')).toHaveText('1');
  await expect(page.getByRole('heading', { name: 'Unresolved questions' })).toBeVisible();
  await expect(page.getByRole('list', { name: 'Actions available now' })).toContainText('Record evidence');
});

test('UI-025-O2', async ({ page }) => {
  // Absent information is stated as absent.
  await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(page.getByText('No finding is supported or confirmed yet.')).toBeVisible();
  await expect(page.getByText('No open question or unresolved contradiction is recorded.')).toBeVisible();
  await expect(page.getByText('No conclusions recorded.')).toBeVisible();
});

test('UI-025-O3', async ({ page }) => {
  // The Overview links to Causal Analysis, Evidence, Hypotheses and Timeline of the same investigation.
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  const links = page.getByRole('list', { name: 'Go to a section' }).getByRole('link');
  await expect(links).toHaveText(['Causal Analysis', 'Evidence', 'Hypotheses', 'Timeline']);
  const id = new URL(v.url).hash.split('/')[3];
  for (const href of await links.evaluateAll((as) => as.map((a) => a.getAttribute('href')))) expect(href).toContain(id);
});

test('UI-025-O4', async ({ page }) => {
  // Nothing unsupported is presented as established: two linked hypotheses are still "proposed", no cause is named.
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(page.locator('main')).not.toContainText(/root cause is|confirmed cause/i);
  await section(page, 'Hypotheses');
  await expect(page.getByRole('article').filter({ hasText: v.h1 })).toContainText('Hypothesis · proposed');
});
