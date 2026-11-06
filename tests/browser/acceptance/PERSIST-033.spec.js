// Owner: ui (written by ui-2, approved by integration authority). PERSIST-033: the workspace header's connection state and
// the investigations of this tab whose changes GitHub does not have, checked against their own states.
import { test, expect } from '@playwright/test';
import { open, providerEnv, useProvider, submitConnection, forward } from '../support/app.mjs';

// ---- Driving one investigation through the real engine and provider (shared by the ui-2 save-state specs) ----------
// The stateful GitHub fake playwright.config.ts starts (the plan's "test build ... with the stateful fake"), or the
// designated test repository when CAUSA_PROVIDER=github. Without the provider the test fails as BLOCKED (never skipped).
const env = providerEnv;
const field = (page, name) => page.getByLabel(name, { exact: true });
const button = (page, name) => page.getByRole('button', { name, exact: true });
const saveState = (page) => page.getByRole('status', { name: 'Save state' });
const marker = (label) => `${label}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
/// In-page navigation: the engine keeps this tab's state (a reload would start a new tab).
const go = (page, hash) => page.evaluate((h) => { location.hash = h; }, hash);
const isMutation = (request) => request.method() === 'POST' && /\/graphql$/.test(new URL(request.url()).pathname) && (request.postData() || '').includes('createCommitOnBranch');

/// Opens the application and, when it is not connected yet, connects to the provider's repository with the connection
/// form's exact labels (integration contract: "Repository owner", "Repository", "Branch", "Token"; support/app.mjs).
const connected = async (page) => {
  await useProvider(page, 'the save states need a connected provider');
  await open(page, '#/investigations');
  const owner = field(page, 'Repository owner');
  if (await owner.isVisible().catch(() => false)) {
    await submitConnection(page);
    await expect(owner).toBeHidden({ timeout: 20000 });
  }
};
/// Starts a new investigation in this tab; it is Unsaved (J1-03). Returns its values and its workspace hash.
const started = async (page, label = 'save') => {
  await connected(page);
  await go(page, '#/new');
  const v = { problem: marker(`${label}-problem`), title: marker(`${label}-title`) };
  await field(page, 'Problem statement').fill(v.problem);
  await field(page, 'Title').fill(v.title);
  await button(page, 'Start investigation').click();
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 20000 });
  v.hash = new URL(page.url()).hash;
  return v;
};
/// One edit through the Overview (J3's recordObservation).
const edit = async (page, text) => {
  await page.getByRole('navigation', { name: /sections/i }).first().getByRole('link', { name: 'Overview', exact: true }).click();
  await field(page, 'Observation text').fill(text);
  await button(page, 'Record an observation').click();
};
const saveAndConfirm = async (page) => {
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/, { timeout: 30000 });
};
/// The provider refuses the next commit mutation with this status (the request still targets the real path).
const refuseNextMutation = async (page, status = 422) => {
  let used = false;
  await page.route('**/graphql', (route) => {
    if (!used && isMutation(route.request())) { used = true; return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ message: 'refused' }) }); }
    return route.fallback();
  });
};
/// The next commit mutation reaches GitHub, but its answer is lost on the way back (an applied, unknown outcome).
const loseNextAnswer = async (page) => {
  let used = false;
  await page.route('**/graphql', async (route) => {
    if (!used && isMutation(route.request())) { used = true; await forward(route).catch(() => null); return route.abort('connectionreset'); }
    return route.fallback();
  });
};
/// Every reconciliation read fails, so the automatic reads are spent without an answer.
const failReconciliation = (page) => page.route(/\/compare\//, (route) => route.abort('connectionreset'));
/// Holds the next commit mutation until release() is called (Saving is observable meanwhile).
const holdNextMutation = async (page) => {
  let release; const gate = new Promise((r) => { release = r; });
  let used = false;
  await page.route('**/graphql', async (route) => {
    if (!used && isMutation(route.request())) { used = true; await gate; return route.fallback(); }
    return route.fallback();
  });
  return () => release();
};
/// A second writer saves the same investigation from a fresh browser context (J3-05).
const otherWriterSaves = async (browser, hash) => {
  const context = await browser.newContext();
  const other = await context.newPage();
  await connected(other);
  await go(other, hash);
  await edit(other, marker('writer-b'));
  await saveAndConfirm(other);
  await context.close();
};
/// Every text the save indicator and its explanation show (header, operation status, conflict review, save summary).
const indicatorText = (page) => page.locator('.causa-frame__record, .causa-save-panel').allInnerTexts().then((t) => t.join('\n'));
/// The lozenge's every value while the test runs, with the time it was shown.
const recordLozenge = (page) => page.evaluate(() => {
  window.__saveStates = [];
  const seen = () => { const l = document.querySelector('.causa-save__state'); const t = l ? l.textContent.trim() : ''; const last = window.__saveStates.at(-1); if (!last || last.state !== t) window.__saveStates.push({ state: t, at: Date.now() }); };
  new MutationObserver(seen).observe(document.body, { subtree: true, childList: true, characterData: true });
  seen();
});

const summary = (page) => page.getByRole('navigation', { name: /changes GitHub does not have/ });

test('PERSIST-033-O1', async ({ page }) => {
  // The connection state, and with unsent investigations their number and a link to each.
  const a = await started(page, 'o1-a');
  await expect(page.locator('.causa-save-panel__connection')).toContainText(`Connected to ${env.owner}/${env.repo}`);
  await expect(summary(page)).toContainText('1 investigation in this tab');
  await go(page, '#/new');
  await field(page, 'Problem statement').fill(marker('o1-b-problem'));
  const bTitle = marker('o1-b-title');
  await field(page, 'Title').fill(bTitle);
  await button(page, 'Start investigation').click();
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 20000 });
  await expect(summary(page)).toContainText('2 investigations in this tab');
  await expect(summary(page).getByRole('link', { name: a.title })).toHaveAttribute('href', a.hash.replace(/\/[^/]+$/, '/overview'));
  await expect(summary(page).getByRole('link', { name: bTitle })).toBeVisible();
  await summary(page).getByRole('link', { name: a.title }).click();
  await expect(page.getByRole('heading', { name: a.title })).toBeVisible();
});

test('PERSIST-033-O2', async ({ page }) => {
  // The header never contradicts the investigations: the count follows their states, and with all saved it lists none.
  const a = await started(page, 'o2-a');
  await go(page, '#/new');
  await field(page, 'Problem statement').fill(marker('o2-b-problem'));
  await button(page, 'Start investigation').click();
  await expect(summary(page)).toContainText('2 investigations in this tab');
  await saveAndConfirm(page);
  await expect(summary(page)).toContainText('1 investigation in this tab');
  await go(page, a.hash);
  await saveAndConfirm(page);
  await expect(summary(page)).toHaveCount(0);
  await expect(page.locator('main')).not.toContainText(/no investigations|empty/i);
});

test('PERSIST-033-O3', async ({ page }) => {
  // PAT-02 / PAT-05 / PAT-07: the header against the states in a conflict-free save, offline, and a lost answer.
  await started(page, 'o3');
  await saveAndConfirm(page);
  await expect(summary(page)).toHaveCount(0);
  await page.context().setOffline(true);
  await edit(page, marker('o3-offline'));
  await expect(saveState(page)).toHaveText(/^\s*Offline\s*$/, { timeout: 10000 });
  await expect(page.locator('.causa-save-panel__connection')).toContainText('You are offline');
  await expect(summary(page)).toContainText('1 investigation in this tab');
  await page.context().setOffline(false);
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 10000 });
  await failReconciliation(page);
  await loseNextAnswer(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Unknown commit outcome\s*$/, { timeout: 20000 });
  await expect(summary(page)).toContainText('1 investigation in this tab');
});
