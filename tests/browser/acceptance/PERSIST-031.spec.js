// Owner: ui (written by ui-2, approved by integration authority). PERSIST-031: each save state's entry condition, words and
// actions, reached through the real engine and provider (refusals and lost answers injected at the network boundary).
import { test, expect } from '@playwright/test';
import { open, requireGitHub } from '../support/app.mjs';

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
  requireGitHub('the save states need the real provider');
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

const noSaveAction = async (page) => {
  for (const name of ['Save', 'Retry', 'Check again']) await expect(button(page, name)).toHaveCount(0);
};
const text = (page) => page.locator('.causa-save__text');

test('PERSIST-031-O1', async ({ page }) => {
  // Each state is reached from its entry condition, with its words and actions: Unsaved -> Saving -> Saved -> Unsaved.
  await started(page, 'o1');
  await expect(button(page, 'Save')).toBeVisible();
  const release = await holdNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Saving\s*$/);
  release();
  await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/, { timeout: 30000 });
  await edit(page, marker('o1-edit'));
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/);
});

test('PERSIST-031-O2', async ({ page }) => {
  // Saved only after the save was confirmed (PERSIST-007): never while the commit mutation is unanswered.
  await started(page, 'o2');
  await recordLozenge(page);
  let answeredAt = null;
  page.on('response', (r) => { if (answeredAt === null && isMutation(r.request())) answeredAt = Date.now(); });
  await saveAndConfirm(page);
  const savedAt = await page.evaluate(() => (window.__saveStates.find((s) => s.state === 'Saved') || {}).at);
  expect(answeredAt, 'the commit mutation was answered').not.toBeNull();
  expect(savedAt, 'Saved appears only after the commit mutation was answered').toBeGreaterThanOrEqual(answeredAt);
  await expect(button(page, 'Save')).toHaveCount(0);
});

test('PERSIST-031-O3', async ({ page }) => {
  // The lozenge reads "Saved" and the freshness line gives the commit time.
  await started(page, 'o3');
  await saveAndConfirm(page);
  await expect(page.locator('.causa-save__state')).toHaveText('Saved');
  await expect(page.locator('.ef-record-header .ef-freshness time')).toHaveText(/\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC/);
});

test('freshness shows commit time', async ({ page }) => {
  await started(page, 'fresh');
  await expect(page.locator('.ef-record-header .ef-freshness')).toContainText('None in this tab');
  const before = Date.now();
  await saveAndConfirm(page);
  const iso = await page.locator('.ef-record-header .ef-freshness time').getAttribute('datetime');
  const t = Date.parse(iso);
  expect(Number.isNaN(t)).toBe(false);
  expect(Math.abs(t - before)).toBeLessThan(10 * 60 * 1000);
});

test('PERSIST-031-O4', async ({ page }) => {
  // Saved offers no action.
  await started(page, 'o4');
  await saveAndConfirm(page);
  await noSaveAction(page);
  await expect(page.locator('.ef-operation-status, .ef-conflict-review')).toHaveCount(0);
});

test('PERSIST-031-O5', async ({ page }) => {
  // Unsaved: unsent changes while online.
  await started(page, 'o5');
  await saveAndConfirm(page);
  await edit(page, marker('o5-edit'));
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/);
  expect(await page.evaluate(() => navigator.onLine)).toBe(true);
});

test('PERSIST-031-O6', async ({ page }) => {
  await started(page, 'o6');
  await expect(text(page)).toContainText('only in this tab');
});

test('PERSIST-031-O7', async ({ page }) => {
  // The action is Save, and it sends the change.
  await started(page, 'o7');
  const mutation = page.waitForRequest(isMutation);
  await button(page, 'Save').click();
  await mutation;
  await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/, { timeout: 30000 });
});

test('PERSIST-031-O8', async ({ page }) => {
  // Saving while the commit mutation is in flight.
  await started(page, 'o8');
  const release = await holdNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Saving\s*$/);
  release();
});

test('PERSIST-031-O9', async ({ page }) => {
  await started(page, 'o9');
  const release = await holdNextMutation(page);
  await button(page, 'Save').click();
  await expect(text(page)).toContainText('not confirmed');
  await expect(text(page)).toContainText('GitHub');
  release();
});

test('PERSIST-031-O10', async ({ page }) => {
  await started(page, 'o10');
  const release = await holdNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Saving\s*$/);
  await noSaveAction(page);
  release();
});

test('PERSIST-031-O11', async ({ page }) => {
  // Offline: offline with unsent changes; the values stay shown.
  await started(page, 'o11');
  await saveAndConfirm(page);
  await page.context().setOffline(true);
  const kept = marker('o11-offline');
  await edit(page, kept);
  await expect(saveState(page)).toHaveText(/^\s*Offline\s*$/, { timeout: 10000 });
  await expect(page.getByText(kept).first()).toBeVisible();
  await page.context().setOffline(false);
});

test('PERSIST-031-O12', async ({ page }) => {
  await started(page, 'o12');
  await page.context().setOffline(true);
  await expect(saveState(page)).toHaveText(/^\s*Offline\s*$/, { timeout: 10000 });
  await expect(text(page)).toContainText('only in this tab');
  await expect(text(page)).toContainText('until the connection returns');
  await page.context().setOffline(false);
});

test('PERSIST-031-O13', async ({ page }) => {
  // Save is offered only after the connection returns.
  await started(page, 'o13');
  await page.context().setOffline(true);
  await expect(saveState(page)).toHaveText(/^\s*Offline\s*$/, { timeout: 10000 });
  await expect(button(page, 'Save')).toHaveCount(0);
  await page.context().setOffline(false);
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 10000 });
  await expect(button(page, 'Save')).toBeVisible();
});

const conflict = async (page, label) => {
  const v = await started(page, label);
  await saveAndConfirm(page);
  await otherWriterSaves(page.context().browser(), v.hash);
  const mine = marker(`${label}-mine`);
  await edit(page, mine);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Conflict\s*$/, { timeout: 30000 });
  return mine;
};

test('PERSIST-031-O14', async ({ page }) => {
  // Conflict only from another writer's change of this investigation; this tab's change is kept.
  const mine = await conflict(page, 'o14');
  await expect(page.getByText(mine).first()).toBeVisible();
  await expect(page.locator('.ef-conflict-review')).toBeVisible();
});

test('PERSIST-031-O15', async ({ page }) => {
  await conflict(page, 'o15');
  await expect(text(page)).toContainText('Another writer changed this investigation on GitHub');
  await expect(text(page)).toContainText("Nothing of this tab's change was written");
  await expect(text(page)).toContainText('kept');
});

test('PERSIST-031-O16', async ({ page }) => {
  // The three choices of PERSIST-012, once GitHub's version is read; no Save.
  await conflict(page, 'o16');
  for (const name of ['Reapply mine', 'Discard mine', 'Export mine']) await expect(button(page, name)).toBeVisible({ timeout: 20000 });
  await expect(button(page, 'Save')).toHaveCount(0);
  // Discard asks first and changes nothing until confirmed.
  await button(page, 'Discard mine').click();
  await expect(button(page, 'Keep my changes')).toBeVisible();
  await button(page, 'Keep my changes').click();
  await expect(saveState(page)).toHaveText(/^\s*Conflict\s*$/);
});

const unknownOutcome = async (page, label) => {
  await started(page, label);
  await failReconciliation(page);
  await loseNextAnswer(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Unknown commit outcome\s*$/, { timeout: 20000 });
};

test('PERSIST-031-O17', async ({ page }) => {
  // Unknown commit outcome when the mutation's answer is lost; never Saved meanwhile.
  await unknownOutcome(page, 'o17');
  await recordLozenge(page);
  await page.waitForTimeout(3000);
  expect(await page.evaluate(() => window.__saveStates.map((s) => s.state))).not.toContain('Saved');
});

test('PERSIST-031-O18', async ({ page }) => {
  await unknownOutcome(page, 'o18');
  await expect(text(page)).toContainText('may or may not have reached GitHub');
  await expect(text(page)).toContainText('checking');
});

test('PERSIST-031-O19', async ({ page }) => {
  // No Save; Check again only after the automatic reads are spent, and it reads GitHub again.
  await unknownOutcome(page, 'o19');
  await expect(button(page, 'Save')).toHaveCount(0);
  await expect(button(page, 'Check again')).toHaveCount(0);
  await expect(button(page, 'Check again')).toBeVisible({ timeout: 60000 });
  await page.unroute(/\/compare\//);
  const read = page.waitForRequest((r) => /\/compare\//.test(r.url()));
  await button(page, 'Check again').click();
  await read;
});

test('PERSIST-031-O20', async ({ page }) => {
  // Save failed when GitHub refuses the save for a reason other than a stale head.
  await started(page, 'o20');
  await refuseNextMutation(page, 422);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
});

test('PERSIST-031-O21', async ({ page }) => {
  // The text names the cause and says the changes are kept in this tab; the values stay shown.
  const v = await started(page, 'o21');
  await refuseNextMutation(page, 422);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await expect(text(page)).toContainText('refused');
  await expect(text(page)).toContainText('kept in this tab');
  await expect(page.getByText(v.problem).first()).toBeVisible();
});

test('PERSIST-031-O22', async ({ page }) => {
  // Retry (the Save button) when a retry can succeed; Reconnect when the credential was refused.
  await started(page, 'o22');
  await refuseNextMutation(page, 422);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await expect(button(page, 'Reconnect')).toHaveCount(0);
  await button(page, 'Save').first().click();
  await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/, { timeout: 30000 });
  await edit(page, marker('o22-edit'));
  await refuseNextMutation(page, 401);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await expect(button(page, 'Reconnect')).toBeVisible();
  await expect(button(page, 'Save')).toHaveCount(0);
});

test('PERSIST-031-O23', async ({ page }) => {
  // Offline with nothing unsent: the indicator stays Saved and the header shows the application is offline.
  await started(page, 'o23');
  await saveAndConfirm(page);
  await page.context().setOffline(true);
  await expect(page.getByText('You are offline').first()).toBeVisible({ timeout: 10000 });
  await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/);
  await page.context().setOffline(false);
});

test('PERSIST-031-O24', async ({ page }) => {
  // PAT-15 / PAT-12: across one tab's life every state seen is entered from its condition: Saved never while a
  // mutation is unanswered or a change is unsent, Offline only while offline, Unsaved only while online.
  await started(page, 'o24');
  await recordLozenge(page);
  await saveAndConfirm(page);
  await page.context().setOffline(true);
  await edit(page, marker('o24-offline'));
  await expect(saveState(page)).toHaveText(/^\s*Offline\s*$/, { timeout: 10000 });
  await page.context().setOffline(false);
  await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 10000 });
  await refuseNextMutation(page, 422);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  const seen = await page.evaluate(() => window.__saveStates.map((s) => s.state));
  expect(seen).toEqual(['Unsaved', 'Saving', 'Saved', 'Offline', 'Unsaved', 'Saving', 'Save failed']);
});
