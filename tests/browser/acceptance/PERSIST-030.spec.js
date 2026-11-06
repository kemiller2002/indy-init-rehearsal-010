// Owner: ui (written by ui-2, approved by integration authority). PERSIST-030: the Forma save-state indicator in the
// investigation header, driven through the real engine and provider; layout and contrast judged by the pinned Forma kit.
import { test, expect } from '@playwright/test';
import { open, providerEnv, useProvider, submitConnection, forward } from '../support/app.mjs';
import { indicatorComposed, statusVisible, textContrast, nonTextContrast, noHorizontalScroll } from '../support/layout-assertions.mjs';

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

const states = ['Saved', 'Unsaved', 'Saving', 'Offline', 'Conflict', 'Unknown commit outcome', 'Save failed'];

test('PERSIST-030-O1', async ({ page }) => {
  // A status lozenge in the header in every state; Forma freshness for the last confirmed save; Forma operation status
  // for Save failed and Unknown commit outcome; Forma conflict review for Conflict (FL-07 composition).
  await started(page, 'o1');
  expect(await indicatorComposed(page)).toEqual([]);
  await saveAndConfirm(page);
  expect(await indicatorComposed(page)).toEqual([]);
  await expect(page.locator('.ef-record-header .ef-freshness time')).toHaveAttribute('datetime', /\d{4}-\d{2}-\d{2}T/);
  await edit(page, marker('o1-edit'));
  await refuseNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await expect(page.locator('.ef-operation-status')).toBeVisible();
  expect(await indicatorComposed(page)).toEqual([]);
});

test('PERSIST-030-O2', async ({ page }) => {
  // The lozenge paints only colours of the semantic theme tokens (Forma's --ef-color-* and the Causa theme's --causa-*,
  // UI-016), and the states are told apart by them.
  const themeColours = () => page.evaluate(() => {
    const names = new Set();
    for (const sheet of document.styleSheets) {
      let rules; try { rules = sheet.cssRules; } catch { continue; }
      const walk = (list) => { for (const r of list) { if (r.style) for (const n of r.style) if (/^--(ef-color|causa)-/.test(n)) names.add(n); if (r.cssRules) walk(r.cssRules); } };
      walk(rules);
    }
    const probe = document.createElement('span'); document.body.append(probe);
    const out = new Set();
    for (const n of names) { probe.style.color = ''; probe.style.color = `var(${n})`; out.add(getComputedStyle(probe).color.replace(/\s/g, '')); }
    probe.remove();
    return [...out];
  });
  const painted = () => page.locator('.causa-save__state').evaluate((e) => {
    const s = getComputedStyle(e);
    return { color: s.color, background: s.backgroundColor, mark: s.borderInlineStartColor };
  });
  const check = async (state) => {
    const tokens = await themeColours();
    const p = await painted();
    for (const [k, c] of Object.entries(p)) {
      const v = c.replace(/\s/g, '');
      if (/rgba\(0,0,0,0\)|transparent/.test(v)) continue;
      expect(tokens, `${state} lozenge ${k} ${v} is a theme token colour`).toContain(v);
    }
    return p;
  };
  await started(page, 'o2');
  await expect(saveState(page)).toHaveText('Unsaved');
  const unsaved = await check('Unsaved');
  await saveAndConfirm(page);
  const saved = await check('Saved');
  expect(saved, 'Saved and Unsaved are painted differently').not.toEqual(unsaved);
  await edit(page, marker('o2-edit'));
  await refuseNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await check('Save failed');
});

test('PERSIST-030-O3', async ({ page }) => {
  // Exactly one of the seven states at any time, in exactly one header lozenge.
  await started(page, 'o3');
  await recordLozenge(page);
  await saveAndConfirm(page);
  await edit(page, marker('o3-edit'));
  await refuseNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  const seen = await page.evaluate(() => window.__saveStates.map((s) => s.state));
  for (const s of seen) expect(states, `"${s}" is one of the seven states`).toContain(s);
  await expect(page.locator('.ef-record-header .causa-save__state')).toHaveCount(1);
  await expect(saveState(page)).toHaveCount(1);
});

test('PERSIST-030-O4', async ({ page }) => {
  // Each state is named in text and marked with the lozenge glyph, never by colour alone.
  const glyph = () => page.locator('.causa-save__state').evaluate((e) => getComputedStyle(e, '::before').content);
  await started(page, 'o4');
  await expect(saveState(page)).toHaveText('Unsaved');
  const unsaved = await glyph();
  expect(unsaved).not.toMatch(/^(none|normal|"")$/);
  await saveAndConfirm(page);
  const saved = await glyph();
  expect(saved).not.toMatch(/^(none|normal|"")$/);
  expect(saved).not.toBe(unsaved);
});

test('PERSIST-030-O5', async ({ page }) => {
  // Text 4.5:1 and the indicator's non-text marks 3:1, in Unsaved, Saved and Save failed.
  await started(page, 'o5');
  expect(await textContrast(page)).toEqual([]);
  expect(await nonTextContrast(page)).toEqual([]);
  await saveAndConfirm(page);
  expect(await textContrast(page)).toEqual([]);
  await edit(page, marker('o5-edit'));
  await refuseNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  expect(await textContrast(page)).toEqual([]);
  expect(await nonTextContrast(page)).toEqual([]);
});

test('PERSIST-030-O6', async ({ page }) => {
  // The indicator is a polite status region; Save failed (like Conflict and Unknown commit outcome) is also announced
  // assertively.
  await started(page, 'o6');
  const lozenge = page.locator('.causa-save__state');
  await expect(lozenge).toHaveAttribute('role', 'status');
  expect(await lozenge.getAttribute('aria-live')).not.toBe('off');
  const assertive = page.locator('[aria-live="assertive"]').filter({ hasText: 'Save failed' });
  await expect(assertive).toHaveCount(0);
  await refuseNextMutation(page);
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 20000 });
  await expect(assertive).toHaveCount(1);
});

test('PERSIST-030-O7', async ({ page }) => {
  // Visible and readable at 320 px, at 200% text size and in iPad portrait and landscape.
  await started(page, 'o7');
  for (const size of [{ width: 320, height: 640 }, { width: 820, height: 1180 }, { width: 1180, height: 820 }]) {
    await page.setViewportSize(size);
    await page.evaluate(() => window.scrollTo(0, 0));
    expect(await statusVisible(page), `${size.width}x${size.height}`).toEqual([]);
    expect(await noHorizontalScroll(page), `${size.width}x${size.height}`).toEqual([]);
  }
  await page.setViewportSize({ width: 320, height: 640 });
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; window.scrollTo(0, 0); });
  expect(await statusVisible(page), '200% text').toEqual([]);
  expect(await noHorizontalScroll(page), '200% text').toEqual([]);
});

test('PERSIST-030-O8', async ({ page }, testInfo) => {
  // PAT-15: every reachable state rendered, composed and captured from this build.
  const capture = async (state) => {
    await expect(saveState(page)).toHaveText(new RegExp(`^\\s*${state}\\s*$`, 'i'), { timeout: 30000 });
    expect(await indicatorComposed(page), state).toEqual([]);
    expect(await textContrast(page), state).toEqual([]);
    await testInfo.attach(`save-state-${state.replace(/ /g, '-').toLowerCase()}`, { body: await page.screenshot(), contentType: 'image/png' });
  };
  await started(page, 'o8');
  await capture('Unsaved');
  const release = await holdNextMutation(page);
  await button(page, 'Save').click();
  await capture('Saving');
  release();
  await capture('Saved');
  await page.context().setOffline(true);
  await edit(page, marker('o8-offline'));
  await capture('Offline');
  await page.context().setOffline(false);
  await capture('Unsaved');
  await refuseNextMutation(page);
  await button(page, 'Save').click();
  await capture('Save failed');
  await failReconciliation(page);
  await loseNextAnswer(page);
  await button(page, 'Save').click();
  await capture('Unknown commit outcome');
  // Conflict: a second writer saves the same investigation, then this tab saves its own change.
  const w = await started(page, 'o8-conflict');
  await saveAndConfirm(page);
  await otherWriterSaves(page.context().browser(), w.hash);
  await edit(page, marker('o8-mine'));
  await button(page, 'Save').click();
  await capture('Conflict');
});
