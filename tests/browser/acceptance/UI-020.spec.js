// Owner: ui. UI-020: top-level navigation to Investigations, Administration and Settings (T16).
import { test, expect } from '@playwright/test';
import { open, primary, viewports, destinations, routes } from '../support/app.mjs';
import { navigationReachable, navigationCollapses } from '../support/layout-assertions.mjs';

test('UI-020-O1', async ({ page }) => {
  // Clear, accessible top-level navigation: a labelled navigation landmark whose links name the three destinations,
  // each opening its own page with its own heading.
  await open(page, '#/investigations');
  for (const name of destinations) {
    const nav = await primary(page);
    await expect(nav).toHaveAttribute('aria-label', 'Primary');
    await nav.getByRole('link', { name, exact: true }).click();
    await expect(page).toHaveTitle(`${name} · Causa`);
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
  }
});

test('UI-020-O2', async ({ page }) => {
  // Operational information is on Administration, preferences on Settings, and neither is on the other or on
  // Investigations.
  await open(page, routes.Administration);
  const main = page.locator('main');
  await expect(main.getByRole('term').filter({ hasText: 'Provider' })).toBeVisible();
  await expect(main.getByText('GitHub (api.github.com)')).toBeVisible();
  await expect(main.getByRole('radio')).toHaveCount(0);
  await open(page, routes.Settings);
  await expect(main.getByRole('radiogroup').or(main.getByRole('group')).first()).toBeVisible();
  await expect(main.getByRole('radio', { name: 'Reduce motion' })).toBeVisible();
  await expect(main.getByRole('term')).toHaveCount(0);
  await open(page, routes.Investigations);
  await expect(main.getByRole('radio')).toHaveCount(0);
  await expect(main.getByRole('term')).toHaveCount(0);
});

test('UI-020-O3', async ({ page }) => {
  // Desktop, iPad and mobile: the navigation is reachable by keyboard, marks the active page, and collapses into
  // Forma's compact menu at narrow widths.
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await open(page, routes.Settings);
    expect(await navigationCollapses(page), `${vp.id}: collapse`).toEqual([]);
    expect(await navigationReachable(page), `${vp.id}: reachable`).toEqual([]);
    const nav = await primary(page);
    await expect(nav.getByRole('link', { name: 'Settings', exact: true }), vp.id).toHaveAttribute('aria-current', 'page');
    await expect(nav.locator('[aria-current="page"]'), vp.id).toHaveCount(1);
    // Keyboard: focus Administration and press Enter.
    await nav.getByRole('link', { name: 'Administration', exact: true }).focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { level: 1, name: 'Administration' }), vp.id).toBeVisible();
  }
  // Assistive technology: the landmark, its links and the current page are in the accessibility tree.
  await page.setViewportSize({ width: 1440, height: 900 });
  await open(page, routes.Administration);
  const snapshot = await page.locator('nav.ef-navigation-shell__persistent').ariaSnapshot();
  expect(snapshot).toContain('navigation "Primary"');
  expect(snapshot).toMatch(/link "Administration"/);
});

test('UI-020-O4', async ({ page }) => {
  // Each destination is its own page with distinct content, not one dashboard showing everything.
  const seen = [];
  for (const name of destinations) {
    await open(page, routes[name]);
    const text = await page.locator('main').innerText();
    expect(text).toContain(name);
    seen.push(text);
  }
  expect(new Set(seen).size).toBe(3);
  await open(page, routes.Investigations);
  await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1);
  await expect(page.getByText('Storage contract')).toHaveCount(0);
});
