// Owner: ui. UI-031: moving detail out of investigation views keeps it in Administration; Settings holds preferences.
import { test, expect } from '@playwright/test';
import { open, routes } from '../support/app.mjs';
import { connect, env, investigationWithContent, section } from './workspace-data.mjs';

const fact = (page, label) => page.locator('.causa-fact').filter({ hasText: label }).first();

test('UI-031-O1', async ({ page }) => {
  // Observability is kept: Administration lists diagnostics (or says none were recorded).
  await open(page, routes.Administration);
  await expect(page.getByRole('heading', { name: 'Diagnostics' })).toBeVisible();
  await expect(page.getByText('No problems have been recorded in this tab.')).toBeVisible();
});

test('UI-031-O2', async ({ page }) => {
  // Provider, repository, catalog, storage contract, versions, data validation and connection health are retained.
  await connect(page);
  await open(page, routes.Administration);
  for (const label of ['Provider', 'Repository', 'Connection', 'Catalog', 'Storage contract', 'Application version', 'Deployed commit', 'Data validation']) await expect(fact(page, label), label).toBeVisible();
  await expect(fact(page, 'Repository')).toContainText(env.repo);
});

test('UI-031-O3', async ({ page }) => {
  // Evidence keeps its source attribution and who recorded it.
  const v = await investigationWithContent(page);
  await section(page, 'Evidence');
  const card = page.getByRole('article').filter({ hasText: v.e2 }).first();
  await expect(card).toContainText(`Source: ${v.s2}`);
  await expect(card).toContainText(/Recorded by \S+ at /);
});

test('UI-031-O4', async ({ page }) => {
  // Settings holds only adjustable options; read-only technical facts are not there.
  await open(page, routes.Settings);
  const main = await page.locator('main').innerText();
  expect(main).not.toMatch(/Storage contract|Deployed commit|Application version|Data validation/);
  await expect(page.getByRole('radio').first()).toBeEnabled();
});

test('UI-031-O5', async ({ page }) => {
  // Single-user scope: no role management; the credential is never shown.
  await connect(page);
  for (const route of [routes.Administration, routes.Settings, routes.Investigations]) {
    await open(page, route);
    await expect(page.getByRole('heading', { level: 1 }), route).toBeVisible();
    await expect(page.locator('main'), route).not.toContainText(/\brole(s)?\b.*(admin|member)|Manage users/i);
    expect(await page.content(), route).not.toContain(env.token);
  }
});
