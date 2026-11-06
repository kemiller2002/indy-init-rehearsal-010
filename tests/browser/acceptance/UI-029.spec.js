// Owner: ui. UI-029: persistent primary destinations and an investigation's own section navigation (T16).
import { test, expect } from '@playwright/test';
import { open, primary, viewports, destinations, catalogInvestigation } from '../support/app.mjs';

const sectionNames = ['Overview', 'Causal Analysis', 'Evidence', 'Hypotheses', 'Timeline'];
const sections = (page) => page.getByRole('navigation', { name: 'Investigation sections' });

test('UI-029-O1', async ({ page }) => {
  // The three destinations are present, and distinct, on every kind of page (list, settings, an investigation, an
  // unknown route).
  for (const route of ['#/investigations', '#/settings', catalogInvestigation, '#/no-such-page']) {
    await open(page, route);
    const nav = await primary(page);
    for (const name of destinations) await expect(nav.getByRole('link', { name, exact: true }), route).toHaveCount(1);
    const hrefs = await nav.getByRole('link').evaluateAll((links) => links.map((a) => a.getAttribute('href')));
    expect(new Set(hrefs).size, route).toBe(3);
  }
});

test('UI-029-O2', async ({ page }) => {
  // Within an investigation, the section navigation offers Overview, Causal Analysis, Evidence, Hypotheses and
  // Timeline, each opening that section of the same investigation. Corrective Actions is offered only where supported
  // (no backend supports it yet, so it is absent).
  await open(page, catalogInvestigation);
  await expect(sections(page).getByRole('link')).toHaveText(sectionNames);
  await expect(sections(page).getByRole('link', { name: 'Corrective Actions' })).toHaveCount(0);
  for (const name of sectionNames) {
    await sections(page).getByRole('link', { name, exact: true }).click();
    await expect(sections(page).getByRole('link', { name, exact: true })).toHaveAttribute('aria-current', 'page');
    await expect(page).toHaveTitle(`${name} · Investigation · Causa`);
    expect(page.url()).toContain('6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11');
  }
  // Outside an investigation there is no section navigation.
  await open(page, '#/investigations');
  await expect(sections(page)).toHaveCount(0);
});

test('UI-029-O3', async ({ page }) => {
  // The current destination and section are indicated at every viewport, and focus stays usable: a section link
  // activated by keyboard keeps the page navigable and the current marker moves.
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await open(page, catalogInvestigation);
    const nav = await primary(page);
    await expect(nav.getByRole('link', { name: 'Investigations', exact: true }), vp.id).toHaveAttribute('aria-current', 'page');
    await expect(sections(page).getByRole('link', { name: 'Overview', exact: true }), vp.id).toHaveAttribute('aria-current', 'page');
    await sections(page).getByRole('link', { name: 'Timeline', exact: true }).focus();
    await page.keyboard.press('Enter');
    await expect(sections(page).getByRole('link', { name: 'Timeline', exact: true }), vp.id).toHaveAttribute('aria-current', 'page');
    await expect(sections(page).getByRole('link', { name: 'Overview', exact: true }), vp.id).toHaveAttribute('aria-current', 'false');
    const focused = await page.evaluate(() => document.activeElement && document.activeElement.textContent);
    expect(focused, `${vp.id}: focus is not lost`).toBe('Timeline');
  }
});

test('UI-029-O4', async ({ page }) => {
  // Global and investigation destinations are two separate navigations; Settings is never inside the investigation's.
  await open(page, catalogInvestigation);
  const nav = await primary(page);
  await expect(sections(page)).toBeVisible();
  expect(await nav.evaluate((n, s) => n === s || n.contains(s) || s.contains(n), await sections(page).elementHandle())).toBe(false);
  for (const name of destinations) await expect(sections(page).getByRole('link', { name, exact: true })).toHaveCount(0);
  for (const name of sectionNames) await expect(nav.getByRole('link', { name, exact: true })).toHaveCount(0);
});
