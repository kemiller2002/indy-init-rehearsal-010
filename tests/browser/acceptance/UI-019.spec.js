// Owner: ui-2 (T29). UI-019: a separate Settings page with only the preferences this build honours (motion, text
// size), each with its scope and effective value; the data repository connection shown apart as a verified fact;
// no credential is a setting, stored or shown.
import { test, expect } from '@playwright/test';
import { open, load, started, primary, routes } from '../support/app.mjs';

const main = (page) => page.locator('main');

test('UI-019-O1', async ({ page }) => {
  // Its own destination, distinct from Administration and the investigation views.
  await open(page, routes.Investigations);
  await (await primary(page)).getByRole('link', { name: 'Settings', exact: true }).click();
  await expect(page).toHaveURL(/#\/settings$/);
  await expect(page).toHaveTitle('Settings · Causa');
  await expect(page.getByRole('heading', { level: 1, name: 'Settings' })).toBeVisible();
  await expect(main(page).getByRole('term')).toHaveCount(0);
  await expect(main(page)).not.toContainText('Storage contract');
});

test('UI-019-O2', async ({ page }) => {
  // Only supported settings: the motion and text-size preferences, nothing else to set.
  await open(page, routes.Settings);
  const groups = main(page).getByRole('group');
  await expect(groups).toHaveCount(2);
  await expect(groups.nth(0)).toContainText('Motion');
  await expect(groups.nth(1)).toContainText('Text size');
  await expect(main(page).getByRole('radio')).toHaveCount(4);
  await expect(main(page).locator('input:not([type=radio]), select, textarea')).toHaveCount(0);
});

test('UI-019-O3', async ({ page }) => {
  // The connection is shown apart from the preferences, with its status as a fact, and is not editable here.
  await open(page, routes.Settings);
  const connection = main(page).locator('section.causa-settings__connection');
  await expect(connection.getByRole('heading', { name: 'Data repository' })).toBeVisible();
  await expect(connection.locator('.ef-status-lozenge')).toHaveText(/Needs attention|Known|Verified|Unknown|Failed/);
  await expect(connection.locator('input, select, textarea, button')).toHaveCount(0);
  await expect(connection).toContainText('verified on Investigations');
  await expect(main(page).getByRole('form', { name: 'Preferences' }).locator('section.causa-settings__connection')).toHaveCount(0);
});

test('UI-019-O4', async ({ page }) => {
  // Every control is honoured: Larger enlarges the text, Reduce motion turns motion off.
  await open(page, routes.Settings);
  const app = page.locator('.causa-app');
  const size = () => page.getByRole('heading', { level: 1, name: 'Settings' }).evaluate((e) => parseFloat(getComputedStyle(e).fontSize));
  const before = await size();
  await main(page).getByRole('radio', { name: 'Larger' }).check();
  await expect(app).toHaveAttribute('data-text-size', 'large');
  expect(await size()).toBeGreaterThan(before);
  await main(page).getByRole('radio', { name: 'Reduce motion' }).check();
  await expect(app).toHaveAttribute('data-motion', 'reduce');
  const transition = await page.locator('main a, main button').first().evaluate((e) => getComputedStyle(e).transitionDuration).catch(() => '0s');
  expect(transition.split(',').every((d) => parseFloat(d) === 0)).toBe(true);
});

test('UI-019-O5', async ({ page }) => {
  // Each setting states its scope and effective value; the scope is true (this tab: a reload returns to the default);
  // nothing is put in browser storage.
  await open(page, routes.Settings);
  await expect(main(page)).toContainText('In effect: Follow the device setting');
  await expect(main(page)).toContainText('In effect: Default');
  await expect(main(page).getByText(/Scope: This browser tab only/)).toHaveCount(2);
  await main(page).getByRole('radio', { name: 'Larger' }).check();
  await expect(main(page)).toContainText('In effect: Larger');
  const stored = await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) }));
  expect(stored).toEqual({ local: [], session: [] });
  await load(page, routes.Settings);
  await started(page);
  await expect(main(page)).toContainText('In effect: Default');
  await expect(main(page).getByRole('radio', { name: 'Default' })).toBeChecked();
});

test('UI-019-O6', async ({ page }) => {
  // Provider authentication is not a setting: no token or password field on Settings, the token is said to be kept
  // only in this tab's memory, and no token-like value is shown or stored.
  await open(page, routes.Settings);
  await expect(main(page).locator('input[type=password], [autocomplete*=password]')).toHaveCount(0);
  await expect(main(page).getByLabel(/token|credential/i)).toHaveCount(0);
  await expect(main(page)).toContainText('never shown or stored');
  await expect(main(page)).not.toContainText(/ghp_|github_pat_|Bearer /);
  const stored = await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage }));
  expect(stored).not.toMatch(/ghp_|github_pat_|token/i);
});
