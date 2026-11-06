// Owner: ui-2 (T29). UI-018: a separately navigable, read-only Administration page for system information and
// diagnostics; every fact states whether it is verified, known, unknown, unavailable or failed; versions are the loaded
// ones (the served dist/build-info.json, VER-007); nothing of it leaks into investigation screens.
import { test, expect } from '@playwright/test';
import { open, primary, routes, appUrl, workingCopyInvestigation } from '../support/app.mjs';

const words = ['Verified', 'Known', 'Unknown', 'Unavailable', 'Failed', 'Needs attention'];
/// Every fact on the page: label -> { value, status }.
const facts = async (page) => {
  const rows = page.locator('main .causa-fact');
  await expect(rows.first()).toBeVisible();
  const out = {};
  for (const row of await rows.all()) {
    const label = ((await row.locator('dt').textContent()) || '').trim();
    out[label] = { value: ((await row.locator('.causa-fact__value').textContent()) || '').trim(), status: ((await row.locator('.ef-status-lozenge').textContent()) || '').trim() };
  }
  return out;
};
/// The build record the serving origin holds for this build (what the engine must read).
const served = (page) => page.evaluate(async (u) => { const r = await fetch(u, { cache: 'no-store' }); return r.ok ? r.json() : null; }, appUrl('').replace(/#.*$/, '') + 'build-info.json');

test('UI-018-O1', async ({ page }) => {
  // Its own destination: reached from the primary navigation, its own route, title and heading.
  await open(page, routes.Investigations);
  await (await primary(page)).getByRole('link', { name: 'Administration', exact: true }).click();
  await expect(page).toHaveURL(/#\/administration$/);
  await expect(page).toHaveTitle('Administration · Causa');
  await expect(page.getByRole('heading', { level: 1, name: 'Administration' })).toBeVisible();
  await expect(page.locator('main').getByRole('term').filter({ hasText: 'Provider' })).toBeVisible();
});

test('UI-018-O2', async ({ page }) => {
  // The facts UI-018 lists, each present with a value and a status word; the versions are those of the served build.
  await open(page, routes.Administration);
  const f = await facts(page);
  for (const label of ['Provider', 'Repository', 'Connection', 'Authentication', 'Active catalog', 'Catalog source', 'Storage contract',
    'Domain contract', 'Application version', 'Deployed commit', 'Build gate', 'Limen (browser kernel)', 'Forma (design system)',
    'Folio (print components)', '.NET runtime', 'Data validation and replay']) {
    expect(f[label], label).toBeDefined();
    expect(words, `${label} status`).toContain(f[label].status);
  }
  expect(f['Provider'].value).toBe('GitHub (api.github.com)');
  expect(f['Storage contract'].value).toBe('causa-storage-v1.0.0');
  expect(f['.NET runtime'].value).toMatch(/^\.NET \d+\.\d+\.\d+/);
  const build = await served(page);
  expect(build, 'the build serves build-info.json').not.toBeNull();
  // The engine reads the served record (CR-T29-BUILD): the versions shown are the ones this build recorded.
  await expect.poll(async () => (await facts(page))['Deployed commit'].value, { timeout: 15000 }).toBe(build.commit);
  const now = await facts(page);
  expect(now['Application version'].value).toBe(`Causa build ${build.commit.slice(0, 7)}`);
  const version = (name) => (build.packages.find((p) => p.name === name) || {}).version;
  expect(now['Limen (browser kernel)'].value).toBe(version('@echelon-foundry/limen'));
  expect(now['Forma (design system)'].value).toBe(version('@echelon-foundry/design-system'));
  expect(now['Build gate'].status).toBe(build.gate.verdict === 'PASS' ? 'Verified' : 'Failed');
  // No secret is shown.
  await expect(page.locator('main')).not.toContainText(/ghp_|github_pat_|Bearer /);
});

test('UI-018-O3', async ({ page }) => {
  // Unknown, unavailable, failed and verified are different words with different marks; absence is never a success.
  await open(page, routes.Administration);
  const f = await facts(page);
  const statuses = Object.values(f).map((x) => x.status);
  for (const s of statuses) expect(words).toContain(s);
  expect(f['Data validation and replay'].status).toBe('Unknown');
  const lozenge = (label) => page.locator('main .causa-fact').filter({ has: page.getByRole('term').filter({ hasText: new RegExp(`^${label.replace(/[.()]/g, '\\$&')}$`) }) }).locator('.ef-status-lozenge');
  await expect(lozenge('Data validation and replay')).toHaveAttribute('data-state', 'unknown');
  await expect(lozenge('Provider')).toHaveAttribute('data-state', 'ok');
  const glyph = (l) => l.evaluate((e) => getComputedStyle(e, '::before').content);
  expect(await glyph(lozenge('Data validation and replay'))).not.toBe(await glyph(lozenge('Provider')));
});

test('UI-018-O4', async ({ page }) => {
  // Never implies an unverified operation passed: before connecting, nothing about the connection, the token or the
  // data is Verified.
  await open(page, routes.Administration);
  const f = await facts(page);
  for (const label of ['Connection', 'Authentication', 'Data validation and replay', 'Active catalog', 'Catalog source']) expect(f[label].status, label).not.toBe('Verified');
  expect(f['Connection'].status).toBe('Needs attention');
  // No fact that is not Verified claims a success in its words.
  for (const [label, x] of Object.entries(f)) if (x.status !== 'Verified') expect(x.value, label).not.toMatch(/\b(passed|succeeded|verified|confirmed|accepted|healthy)\b/i);
});

test('UI-018-O5', async ({ page }) => {
  // System diagnostics and versions stay on Administration: not on Investigations, the start screen or a workspace.
  await open(page, routes.Administration);
  const f = await facts(page);
  const leaks = ['Storage contract', 'Deployed commit', 'Build gate', 'Diagnostics', '.NET runtime', 'causa-storage-v1.0.0', f['.NET runtime'].value];
  for (const route of [routes.Investigations, '#/new', workingCopyInvestigation, routes.Settings]) {
    await open(page, route);
    const text = await page.locator('body').innerText();
    for (const l of leaks) expect(text, `${route} shows ${l}`).not.toContain(l);
  }
});

test('UI-018-O6', async ({ page }) => {
  // Read-only: no field, choice or action on the page (only links).
  await open(page, routes.Administration);
  const section = page.locator('main .causa-administration');
  await expect(section).toBeVisible();
  await expect(section.locator('input, select, textarea, button, [data-event]')).toHaveCount(0);
  await expect(section).toContainText('read-only');
});
