// Owner: ui. UI-022: header, footer and catalog cards carry investigation meaning, not build metadata.
import { test, expect } from '@playwright/test';
import { open, routes } from '../support/app.mjs';
import { investigationWithContent, connect } from './workspace-data.mjs';

const sha = /\b[0-9a-f]{7,40}\b/;

test('UI-022-O1', async ({ page }) => {
  // The header shows the application identity and navigation, no commit hash or provider internals.
  for (const route of [routes.Investigations, routes.Administration, routes.Settings]) {
    await open(page, route);
    const header = await page.getByRole('banner').innerText();
    expect(header).toContain('Causa');
    expect(header, route).not.toMatch(sha);
    expect(header, route).not.toMatch(/api\.github\.com|commit/i);
  }
});

test('UI-022-O2', async ({ page }) => {
  // No footer carries build SHAs, component inventories or build times.
  // Whatever the page puts outside its content (header, navigation, any footer) carries no build metadata.
  for (const route of [routes.Investigations, routes.Settings]) {
    await open(page, route);
    const chrome = await page.locator('body').evaluate((b) => {
      const copy = b.cloneNode(true);
      copy.querySelectorAll('main, template, script, style').forEach((e) => e.remove());
      return copy.textContent;
    });
    expect(chrome, route).toContain('Investigations');
    expect(chrome, route).not.toMatch(sha);
    expect(chrome, route).not.toMatch(/\bbuild\b|Limen|Forma|\.NET|\d{4}-\d\d-\d\dT\d\d:\d\d/i);
  }
});

test('UI-022-O3', async ({ page }) => {
  // Catalog cards lead with the title, then status, activity, counts and a clear Open investigation action.
  const v = await investigationWithContent(page);
  await open(page, routes.Investigations);
  const card = page.getByRole('article', { name: v.title });
  await expect(card.getByRole('heading', { level: 2 })).toHaveText(v.title);
  await expect(card.getByRole('link', { name: 'Open investigation' })).toBeVisible();
  await expect(card).toContainText('Status');
});

test('UI-022-O4', async ({ page }) => {
  // No raw case keys as headings, no "Expected lifecycle", no replay explanations.
  await investigationWithContent(page);
  await open(page, routes.Investigations);
  const main = await page.locator('main').innerText();
  expect(main).not.toMatch(/Expected lifecycle|replay|Declared/i);
  const headings = await page.locator('main h2').allTextContents();
  expect(headings.length).toBeGreaterThan(0);
  for (const h of headings) expect(h).not.toMatch(/^[0-9a-f-]{36}$/);
});

test('UI-022-O5', async ({ page }) => {
  // Identifiers stay discoverable for reference: the route names the investigation id, and Administration has facts.
  const v = await investigationWithContent(page);
  expect(v.url).toMatch(/\/[0-9a-f-]{36}\//);
  await open(page, routes.Administration);
  await expect(page.locator('.causa-fact').filter({ hasText: 'Repository' }).first()).toBeVisible();
});

test('UI-022-O6', async ({ page, browser }) => {
  // In a fresh tab the listed investigation has not been opened: its counts are not known, never zero.
  const v = await investigationWithContent(page);
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByRole('status', { name: /save state/i })).toHaveText(/^\s*Saved\s*$/, { timeout: 20000 });
  const fresh = await (await browser.newContext()).newPage();
  await connect(fresh);
  const card = fresh.getByRole('article', { name: v.title });
  await expect(card).toContainText('Not opened yet');
  for (const term of ['Hypotheses', 'Evidence', 'Unresolved contradictions']) {
    await expect(card.locator(`dt:text-is("${term}") + dd`)).toHaveText('Not known until opened');
  }
});
