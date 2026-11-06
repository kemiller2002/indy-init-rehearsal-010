// Owner: ui (written by ui-2, approved by integration authority). INV-25: a refused credential never loses this tab's
// unsaved work: Save failed offers Reconnect, and after reconnecting the same changes are still there and save.
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

test('reconnect keeps unsaved work', async ({ page }) => {
  const v = await started(page, 'inv25');
  const kept = marker('inv25-kept');
  await edit(page, kept);
  await refuseNextMutation(page, 401);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await expect(page.locator('.causa-save__text')).toContainText('credential');
  await expect(page.getByText(kept).first()).toBeVisible();
  await button(page, 'Reconnect').click();
  // The connection form, with the work kept in the tab meanwhile.
  await expect(field(page, 'Token')).toBeVisible({ timeout: 20000 });
  await field(page, 'Token').fill(env.token);
  // The form's own submit button: "Reconnect" also matches /connect/i and may still be shown.
  await page.getByRole('button', { name: 'Connect data repository', exact: true }).click();
  await go(page, v.hash);
  await expect(page.getByText(kept).first()).toBeVisible({ timeout: 20000 });
  await expect(saveState(page)).not.toHaveText(/^\s*Saved\s*$/);
  await saveAndConfirm(page);
});
