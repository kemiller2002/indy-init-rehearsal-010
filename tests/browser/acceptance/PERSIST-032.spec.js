// Owner: ui (written by ui-2, approved by integration authority). PERSIST-032: save-state language, checked on the text the
// application shows in every reachable state.
import { test, expect } from '@playwright/test';
import { open } from '../support/app.mjs';

// ---- Driving one investigation through the real engine and provider (shared by the ui-2 save-state specs) ----------
// The designated test repository, as the governed journeys name it. Without it the provider cannot be reached: the
// test fails as BLOCKED (never skipped, never answered by a substitute).
const env = {
  token: process.env.CAUSA_GITHUB_TOKEN, owner: process.env.CAUSA_TEST_OWNER, repo: process.env.CAUSA_TEST_REPO,
  branch: process.env.CAUSA_TEST_BRANCH || 'main',
};
const field = (page, name) => page.getByLabel(name, { exact: true });
const button = (page, name) => page.getByRole('button', { name, exact: true });
const saveState = (page) => page.getByRole('status', { name: 'Save state' });
const marker = (label) => `${label}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
/// In-page navigation: the engine keeps this tab's state (a reload would start a new tab).
const go = (page, hash) => page.evaluate((h) => { location.hash = h; }, hash);
const isMutation = (request) => request.method() === 'POST' && /\/graphql$/.test(new URL(request.url()).pathname) && (request.postData() || '').includes('createCommitOnBranch');

/// Opens the application and, when it is not connected yet, connects to the designated test repository with the
/// connection form's labels (integration contract: "Repository owner", "Repository", "Branch", "Token").
const connected = async (page) => {
  await open(page, '#/investigations');
  const owner = field(page, 'Repository owner');
  const connect = page.getByRole('button', { name: /connect/i }).first();
  if (!(await owner.isVisible().catch(() => false)) && (await connect.isVisible().catch(() => false))) await connect.click();
  if (await owner.isVisible().catch(() => false)) {
    if (!env.token || !env.owner || !env.repo) throw new Error('BLOCKED: CAUSA_GITHUB_TOKEN, CAUSA_TEST_OWNER and CAUSA_TEST_REPO are not set; the save states need the real provider.');
    await owner.fill(env.owner);
    await field(page, 'Repository').fill(env.repo);
    await field(page, 'Branch').fill(env.branch);
    await field(page, 'Token').fill(env.token);
    await connect.click();
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
    if (!used && isMutation(route.request())) { used = true; await route.fetch().catch(() => null); return route.abort('connectionreset'); }
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
    if (!used && isMutation(route.request())) { used = true; await gate; return route.continue(); }
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

const reserved = /\b(saved|synced|stored|safe)\b/i;
const promised = /locally|on this device|queued|later/i;
/// Unsaved, Offline and Save failed (and, where the provider allows, Unknown commit outcome) with the page's text.
const throughStates = async (page, each) => {
  const v = await started(page, 'lang');
  await each('Unsaved');
  await page.context().setOffline(true);
  await expect(saveState(page)).toHaveText(/^\s*Offline\s*$/, { timeout: 10000 });
  await each('Offline');
  await page.context().setOffline(false);
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 10000 });
  await refuseNextMutation(page, 422);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await each('Save failed');
  await failReconciliation(page);
  await loseNextAnswer(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Unknown commit outcome\s*$/, { timeout: 20000 });
  await each('Unknown commit outcome');
  return v;
};

test('PERSIST-032-O1', async ({ page }) => {
  // Outside Saved, nothing the indicator, its explanation or the save summary shows uses saved, synced, stored or safe.
  await throughStates(page, async (state) => {
    expect(await indicatorText(page), state).not.toMatch(reserved);
  });
});

test('PERSIST-032-O2', async ({ page }) => {
  // No copy says a change is saved locally, on this device, queued or saved later, in any state.
  await throughStates(page, async (state) => {
    expect(await page.locator('main').innerText(), state).not.toMatch(promised);
  });
});

test('PERSIST-032-O3', async ({ page }) => {
  // Offline and Unsaved say the change exists only in this tab.
  await throughStates(page, async (state) => {
    if (state === 'Offline' || state === 'Unsaved') await expect(page.locator('.causa-save__text'), state).toContainText('only in this tab');
  });
});

test('PERSIST-032-O4', async ({ page }) => {
  // Retry and Check again appear only when they perform a real action: no Check again while automatic reads remain,
  // no Retry outside Save failed, and the Save failed retry sends a commit mutation.
  await started(page, 'o4');
  await expect(button(page, 'Check again')).toHaveCount(0);
  await refuseNextMutation(page, 422);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  const retry = page.waitForRequest(isMutation);
  await button(page, 'Save').first().click();
  await retry;
  await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/, { timeout: 30000 });
  await edit(page, marker('o4-edit'));
  await failReconciliation(page);
  await loseNextAnswer(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Unknown commit outcome\s*$/, { timeout: 20000 });
  await expect(button(page, 'Check again')).toHaveCount(0);
});

test('PERSIST-032-O5', async ({ page }) => {
  // PAT-15 / PAT-14: every state's text against the whole list at once, Saved included (where "saved" is allowed).
  await throughStates(page, async (state) => {
    const shown = await indicatorText(page);
    expect(shown, state).not.toMatch(reserved);
    expect(shown, state).not.toMatch(promised);
  });
});
