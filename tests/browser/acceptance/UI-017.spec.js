// Owner: ui. UI-017: investigation pages speak about the investigation, not the implementation.
import { test, expect } from '@playwright/test';
import { open } from '../support/app.mjs';
import { investigationWithContent, section, technical, contentText } from './workspace-data.mjs';

const investigationPages = ['#/investigations', '#/investigation/workspaces%2Fdemo%2Fhistorical-v1/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview'];
const main = (page) => page.locator('main');

test('UI-017-O1', async ({ page }) => {
  // Investigation pages present the investigation's content, statuses and tasks.
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  await expect(main(page)).toContainText(v.problem);
  await expect(main(page)).toContainText('Investigation state');
  await expect(page.getByRole('heading', { name: 'Next steps' })).toBeVisible();
});

test('UI-017-O2', async ({ page }) => {
  // No implementation explanation, schema version, commit hash or framework on investigation pages.
  for (const route of investigationPages) {
    await open(page, route);
    const text = await contentText(page);
    for (const t of technical) expect(text, `${route} ${t}`).not.toMatch(t);
  }
});

test('UI-017-O3', async ({ page }) => {
  // The catalog/replay paragraph is absent from the investigation catalog.
  await open(page, '#/investigations');
  await expect(page.getByRole('heading', { name: 'Investigations', level: 1 })).toBeVisible();
  await expect(page.getByText(/The active catalog historical-v1/)).toHaveCount(0);
});

test('UI-017-O4', async ({ page }) => {
  // A problem that stops the user says so plainly with a next action; technical detail is in Administration.
  await open(page, '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview');
  await expect(page.getByText('Go to Investigations and open it from the list.')).toBeVisible();
  await open(page, '#/administration');
  await expect(page.getByRole('heading', { name: 'Diagnostics' })).toBeVisible();
  await expect(page.locator('.causa-fact').filter({ hasText: 'Storage contract' })).toContainText('causa-storage v1.0.0');
});

test('UI-017-O5', async ({ page }) => {
  // Provenance and source attribution stay in the evidence view.
  const v = await investigationWithContent(page);
  await section(page, 'Evidence');
  const card = page.getByRole('article').filter({ hasText: v.e1 }).first();
  await expect(card).toContainText(`Source: ${v.s1}`);
  await expect(card).toContainText(/Recorded by /);
});
