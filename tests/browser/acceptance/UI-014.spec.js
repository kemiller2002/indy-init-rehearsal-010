// Owner: ui. UI-014: consistent Causa identity, the loading experience, and the current investigation's context (T16).
import { test, expect } from '@playwright/test';
import { load, open, started, routes, catalogInvestigation, workingCopyInvestigation } from '../support/app.mjs';
import { loadingAnnounced, faultActionable, faultStatesPreserved } from '../support/layout-assertions.mjs';

test('UI-014-O1', async ({ page, request }) => {
  // Naming, icon, titles and favicon: every page is titled "<page> · Causa", shows the Causa mark and name, and the
  // favicon is the same local Causa icon.
  const titles = { [routes.Investigations]: 'Investigations · Causa', [routes.Administration]: 'Administration · Causa', [routes.Settings]: 'Settings · Causa', [catalogInvestigation]: 'Overview · Investigation · Causa', '#/no-such-page': 'Page not found · Causa' };
  for (const [route, title] of Object.entries(titles)) {
    await open(page, route);
    await expect(page).toHaveTitle(title);
    await expect(page.getByRole('banner').getByRole('img', { name: 'Causa' })).toBeVisible();
    await expect(page.getByRole('banner')).toContainText('Causa');
  }
  const icon = await page.locator('link[rel="icon"]').getAttribute('href');
  expect(icon).toBe('causa-icon.svg');
  const served = await request.get(new URL(icon, page.url()).toString());
  expect(served.ok()).toBe(true);
  expect(await served.text()).toContain('aria-label="Causa"');
  // Typography and navigation hierarchy are the same on every destination: one h1, same heading font, same nav.
  const fonts = new Set();
  for (const route of [routes.Investigations, routes.Administration, routes.Settings]) {
    await open(page, route);
    fonts.add(await page.getByRole('heading', { level: 1 }).evaluate((h) => getComputedStyle(h).fontFamily));
  }
  expect(fonts.size).toBe(1);
  // Status colours are accessible and paired with words (the Administration statuses).
  await open(page, routes.Administration);
  const statuses = await page.locator('main .ef-status-lozenge').allInnerTexts();
  expect(statuses.length).toBeGreaterThan(0);
  for (const s of statuses) expect(s.trim()).toMatch(/[A-Za-z]{3,}/);
});

test('UI-014-O2', async ({ page }) => {
  // WASM-LOAD-001/002: the branded loading screen (mark and status) shows before the delayed runtime, announced, with
  // no fabricated progress; WASM-LOAD-004: it stays until the engine is ready, then hands off once.
  await page.route(/_framework\/dotnet\.js$/, async (route) => { await new Promise((r) => setTimeout(r, 2000)); await route.continue(); });
  await load(page, routes.Investigations, { waitUntil: 'commit' });
  const status = page.getByRole('status').filter({ hasText: 'Loading Causa…' });
  await expect(status).toBeVisible();
  await expect(page.locator('.causa-loading .causa-mark')).toBeVisible();
  await expect(page.locator('progress, [role=progressbar]')).toHaveCount(0);
  await expect(page.locator('main')).not.toContainText('%');
  expect(await loadingAnnounced(page)).toEqual([]);
  await started(page);
  await expect(status).toBeHidden();
  await expect(page.getByRole('heading', { level: 1, name: 'Investigations' })).toHaveCount(1);
  // WASM-LOAD-005: a failed runtime shows what happened, what was preserved and a working Retry.
  await page.unroute(/_framework\/dotnet\.js$/);
  await page.route(/_framework\/dotnet\.js$/, (route) => route.abort());
  await load(page, routes.Investigations);
  await expect(page.getByRole('alert')).toContainText('Causa could not start');
  await expect(status).toBeHidden();
  expect(await faultActionable(page)).toEqual([]);
  expect(await faultStatesPreserved(page)).toEqual([]);
  await page.unroute(/_framework\/dotnet\.js$/);
  await page.getByRole('button', { name: 'Retry' }).focus();
  await page.keyboard.press('Enter');
  await started(page);
  await expect(page.getByRole('alert')).toBeHidden();
});

test('UI-014-O3', async ({ page }) => {
  // The navigation tells a frozen demonstration from a writable copy and shows which investigation is open.
  await open(page, catalogInvestigation);
  const context = page.getByRole('region', { name: 'Current investigation' });
  await expect(context).toContainText('Frozen demonstration (read-only)');
  await expect(context).toContainText('workspaces/demo/historical-v1');
  await expect(context).toContainText('6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11');
  await open(page, workingCopyInvestigation);
  await expect(context).toContainText('Working copy (writable)');
  await expect(context).not.toContainText('Frozen demonstration');
  await expect(context).toContainText('workspaces/demo/causa-app-r10');
  // Outside an investigation there is no investigation context.
  await open(page, routes.Settings);
  await expect(context).toHaveCount(0);
});
