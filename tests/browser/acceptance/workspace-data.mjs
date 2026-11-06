// Owner: ui. Driving the integrated application to an investigation with content, for the UI acceptance specs that
// judge investigation content (UI-001, UI-003..005, UI-010, UI-013, UI-023, UI-025, UI-026, UI-030, UI-032..034,
// UI-038). Black-box: by role and accessible name only, the same names the governed journeys use. The provider is the
// stateful GitHub fake playwright.config.ts starts (CAUSA_DOUBLE_URL) unless CAUSA_PROVIDER=github, routed as
// verification/journeys/support.mjs routes its double (support/app.mjs); nothing here is live-write evidence (UI-015-O3).
import { expect } from '@playwright/test';
import { open, providerEnv, requireProvider, useProvider, submitConnection } from '../support/app.mjs';

export const env = providerEnv;
export const marker = (label) => `${label}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`;

export const field = (page, name) => page.getByLabel(name, { exact: true });
export const button = (page, name) => page.getByRole('button', { name, exact: true });
export const sections = (page) => page.getByRole('navigation', { name: 'Investigation sections' });
export const section = async (page, name) => { await sections(page).getByRole('link', { name, exact: true }).click(); };

/// Routes the browser's GitHub requests to the provider (the stateful fake in harness mode) and connects through the
/// application's own form, by its exact labels (support/app.mjs submitConnection).
export const connect = async (page) => {
  await useProvider(page, 'connecting needs the provider');
  await open(page, '#/investigations');
  await submitConnection(page);
  await expect(page.locator('.causa-connection')).toContainText(/Connected/, { timeout: 20000 });
};

/// Starts an investigation and records the J2 content through the commands the engine offers. Returns the values.
export const investigationWithContent = async (page) => {
  const v = {
    problem: marker('problem'), title: marker('title'), question: marker('question'), observation: marker('obs'),
    h1: marker('hyp-a'), h2: marker('hyp-b'), e1: marker('ev-a-content'), e2: marker('ev-b-content'),
    s1: 'https://example.org/' + marker('src'), s2: marker('src-b'), r1: marker('why-supports'), r2: marker('why-contradicts'),
  };
  await connect(page);
  await page.getByRole('link', { name: /new investigation/i }).or(page.getByRole('button', { name: /new investigation/i })).first().click();
  await field(page, 'Problem statement').fill(v.problem);
  await field(page, 'Title').fill(v.title);
  await field(page, 'Primary investigative question').fill(v.question);
  await button(page, 'Start investigation').click();
  await expect(sections(page)).toBeVisible({ timeout: 20000 });
  await section(page, 'Overview');
  await field(page, 'Observation text').fill(v.observation);
  await button(page, 'Record an observation').click();
  await expect(page.getByText(v.observation).first()).toBeVisible();
  await section(page, 'Hypotheses');
  for (const h of [v.h1, v.h2]) { await field(page, 'Hypothesis statement').fill(h); await button(page, 'Record a hypothesis').click(); await expect(page.getByText(h).first()).toBeVisible(); }
  await section(page, 'Evidence');
  for (const [content, source] of [[v.e1, v.s1], [v.e2, v.s2]]) {
    await field(page, 'Evidence label').fill(content);
    await field(page, 'Evidence source reference').fill(source);
    await field(page, 'Evidence original content').fill(content);
    await button(page, 'Record evidence').click();
    await expect(page.getByRole('article').filter({ hasText: content }).first()).toBeVisible();
  }
  for (const [e, kind, why] of [[v.e1, 'supports', v.r1], [v.e2, 'contradicts', v.r2]]) {
    await page.getByRole('combobox', { name: /evidence/i }).first().selectOption({ label: e });
    await page.getByRole('combobox', { name: /hypothes/i }).first().selectOption({ label: v.h1 });
    await page.getByRole('group', { name: /evidence relationship kind/i }).getByLabel(kind, { exact: true }).check();
    await field(page, 'Evidence relationship rationale').fill(why);
    await button(page, 'Link evidence to a hypothesis').click();
    await expect(page.getByText(why).first()).toBeVisible();
  }
  v.url = page.url();
  return v;
};

/// Words that belong in Administration, not on investigation pages (UI-017-O2, UI-022, UI-024-O5).
export const technical = [/\b[0-9a-f]{40}\b/, /causa-storage/i, /schema/i, /\breplay/i, /historical-v1/i, /\bSHA\b/, /\.NET|WebAssembly|wasm/i, /api\.github\.com/i, /catalog identifier/i, /The active catalog historical-v1/];

/// The page's content as read, without the investigation context line, whose workspace and id are the identity the
/// shell states on purpose (UI-014, UI-022-O5).
export const contentText = (page) => page.locator('main').evaluate((m) => {
  const copy = m.cloneNode(true);
  copy.querySelectorAll('[aria-label="Current investigation"] .causa-context__where').forEach((e) => e.remove());
  return copy.innerText ?? copy.textContent;
});

/// Where the provider's REST API is: the double in harness mode, api.github.com otherwise.
export const providerBase = env.provider === 'github' ? 'https://api.github.com' : env.double;

/// Writes a stored causa-storage v1.0.0 history from the vendored contract fixtures into a working copy of the test
/// repository through the provider's contents API (never through the app), and returns the route that opens it. The run
/// defaults to the application's own working copy (workspaces/demo/causa-app-r10, Update.WorkingCopyRun): the application
/// opens a working-copy investigation only after its working-copy listing names it. Used
/// where only a stored history can hold the content (a conclusion: the frozen contract has no command for it).
export const seedStoredHistory = async (fixtureId, run = 'r10') => {
  await requireProvider(env, 'seeding a stored history needs the provider');
  const { readFileSync } = await import('node:fs');
  const workspace = `workspaces/demo/causa-app-${run}`;
  const source = new URL(`../../fixtures/causa-data/e8259df74add723765c57a5b1a5888d9c3f707af/fixtures/v1.0.0/investigations/${fixtureId}/`, import.meta.url);
  const headers = { authorization: `Bearer ${env.token}`, accept: 'application/vnd.github+json', 'content-type': 'application/json' };
  for (const file of ['manifest.json', 'events.ndjson', 'snapshot.json']) {
    const url = `${providerBase}/repos/${env.owner}/${env.repo}/contents/${workspace}/investigations/${fixtureId}/${file}`;
    const existing = await fetch(`${url}?ref=${env.branch}`, { headers });
    const sha = existing.ok ? (await existing.json()).sha : undefined;
    const content = readFileSync(new URL(file, source)).toString('base64');
    const put = await fetch(url, { method: 'PUT', headers, body: JSON.stringify({ message: `seed ${fixtureId}`, content, branch: env.branch, ...(sha ? { sha } : {}) }) });
    if (!put.ok) throw new Error(`seeding ${file} failed: ${put.status}`);
  }
  return `#/investigation/${encodeURIComponent(workspace)}/${fixtureId}/overview`;
};
