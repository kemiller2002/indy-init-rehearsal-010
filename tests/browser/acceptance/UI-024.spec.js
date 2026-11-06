// Owner: ui. UI-024: one presentation language; the default labels; no invented meaning or success claims.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { open, routes } from '../support/app.mjs';
import { investigationWithContent, section } from './workspace-data.mjs';

const replaced = ['Historical investigations', 'Fetching and validating the investigation', 'Expected lifecycle', 'Replayed state', 'Open a sandbox copy', 'Ask the engine', 'Engine decisions', 'Discard the sandbox'];
const unopened = '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview';

test('UI-024-O1', async () => {
  // The words for domain values come from one place (Reporting.fs Words; Views/Language.fs for the shell), and the
  // fragments bind them rather than spelling domain states themselves.
  const reporting = readFileSync(new URL('../../../src/Causa.App/Reporting.fs', import.meta.url), 'utf8');
  expect(reporting).toContain('module Words');
  for (const f of ['workspace-overview', 'workspace-hypotheses', 'workspace-evidence', 'workspace-timeline']) {
    const html = readFileSync(new URL(`../../../web/screens/${f}.html`, import.meta.url), 'utf8');
    expect(html, f).not.toMatch(/>\s*(proposed|plausible|supported|falsified|awaiting evidence)\s*</i);
  }
});

test('UI-024-O2', async ({ page }) => {
  // No invented confidence or success: an investigation without supported findings says so.
  await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(page.getByText('No finding is supported or confirmed yet.')).toBeVisible();
  await expect(page.locator('main')).not.toContainText(/confidence|likely|probably/i);
});

test('UI-024-O3', async ({ page }) => {
  // The replaced labels appear on no page.
  for (const route of [routes.Investigations, routes.Administration, routes.Settings, unopened]) {
    await open(page, route);
    const text = await page.locator('body').innerText();
    for (const label of replaced) expect(text, `${route}: ${label}`).not.toContain(label);
  }
});

test('UI-024-O4', async ({ page }) => {
  // Wording stays true to state: an unopened investigation is "not open", never saved or applied.
  await open(page, unopened);
  const text = await page.locator('main').innerText();
  expect(text).toContain('This investigation is not open.');
  expect(text).not.toMatch(/\bsaved\b|\bapplied\b/i);
});

test('UI-024-O5', async ({ page }) => {
  // Technical codes and provider detail are on Administration only.
  await open(page, routes.Administration);
  await expect(page.locator('main')).toContainText('GitHub (api.github.com)');
  await open(page, routes.Investigations);
  await expect(page.locator('main')).not.toContainText('api.github.com');
});

test('UI-024-O6', async ({ page }) => {
  // Domain terms are kept where they matter: relationship kinds and hypothesis states keep the domain's words.
  const v = await investigationWithContent(page);
  await section(page, 'Hypotheses');
  const card = page.getByRole('article').filter({ hasText: v.h1 });
  await expect(card).toContainText('Hypothesis · proposed');
  await expect(card).toContainText(`supports: ${v.e1}`);
  await expect(card).toContainText(`contradicts: ${v.e2}`);
});
