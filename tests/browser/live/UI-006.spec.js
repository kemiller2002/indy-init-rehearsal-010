// Owner: persistence. UI-006 (live browser): the save-state indicator over the real GitHub test repository
// (CAUSA_GITHUB_TOKEN; BLOCKED, a failure, without it; CAUSA_PROVIDER=harness-double runs it on the stateful fake,
// which is never live evidence). Failures GitHub cannot be made to give on demand (a lost answer, a refusal) are
// injected into real exchanges by the test's own transport.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

const env = {
  app: process.env.CAUSA_APP_URL || '/dist/',
  token: process.env.CAUSA_GITHUB_TOKEN || '',
  owner: process.env.CAUSA_TEST_OWNER || (process.env.CAUSA_TEST_REPOSITORY || 'kemiller2002/causa-acceptance-r10').split('/')[0],
  repo: process.env.CAUSA_TEST_REPO || (process.env.CAUSA_TEST_REPOSITORY || 'kemiller2002/causa-acceptance-r10').split('/')[1],
  branch: process.env.CAUSA_TEST_BRANCH || 'causa-acceptance',
  double: process.env.CAUSA_DOUBLE_URL || '',
  candidate: process.env.CAUSA_CANDIDATE || '',
};
const BLOCKED = 'BLOCKED: CAUSA_GITHUB_TOKEN is not set, so the live browser suite cannot run against the test repository. This is a failure, not a pass: the fake never stands in for GitHub (PERSIST-025-O4).';

/// A provider: the real GitHub API, or the harness double (synthetic, never live evidence).
const providers = {
  live: () => ({ mode: 'live', base: 'https://api.github.com', owner: env.owner, repo: env.repo, token: env.token, from: env.branch }),
  fake: () => ({ mode: 'fake', base: env.double, owner: 'harness', repo: 'causa-test', token: 'harness-token', from: 'main' }),
};

const api = async (p, path, init = {}) => {
  const r = await fetch(p.base + path, { ...init, headers: { Authorization: `Bearer ${p.token}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28', ...(init.headers || {}) } });
  const text = await r.text();
  let body = null;
  try { body = JSON.parse(text); } catch { body = null; }
  return { status: r.status, body, requestId: r.headers.get('x-github-request-id') };
};
const repoPath = (p, rest) => `/repos/${p.owner}/${p.repo}${rest}`;
const headOf = async (p, branch) => (await api(p, repoPath(p, `/git/ref/heads/${encodeURIComponent(branch)}`))).body?.object?.sha;

/// A fresh branch of the provider's repository at its designated branch's head.
const freshBranch = async (p, label) => {
  const name = `causa-live-${label}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
  const from = await headOf(p, p.from);
  if (!from) throw new Error(`cannot read ${p.from} of ${p.owner}/${p.repo}`);
  const made = await api(p, repoPath(p, '/git/refs'), { method: 'POST', body: JSON.stringify({ ref: `refs/heads/${name}`, sha: from }), headers: { 'Content-Type': 'application/json' } });
  if (made.status !== 201 && p.mode === 'live') throw new Error(`cannot create the test branch ${name}: ${made.status}`);
  return name;
};

/// A browser context whose GitHub requests pass through `transport` (pass, hold, lose or refuse each one); in the fake
/// mode they go to the harness double.
const contextFor = async (browser, p, transport = () => 'pass') => {
  const ctx = await browser.newContext();
  const held = [];
  await ctx.route('https://api.github.com/**', async (route) => {
    const req = route.request();
    const u = new URL(req.url());
    const target = p.mode === 'fake' ? p.base + u.pathname + u.search : req.url();
    const what = transport(req);
    if (what === 'refuse') return route.fulfill({ status: 403, contentType: 'application/json', body: JSON.stringify({ message: 'Resource not accessible by integration', documentation_url: 'https://docs.github.com/rest', status: '403' }) });
    const answer = await route.fetch({ url: target }).catch(() => null);
    if (!answer || what === 'lose') return route.abort('connectionreset');
    if (what === 'hold') await new Promise((resolve) => held.push(resolve));
    await route.fulfill({ response: answer });
  });
  ctx.release = () => { while (held.length) held.shift()(); };
  ctx.heldCount = () => held.length;
  return ctx;
};

const isMutation = (req) => req.method() === 'POST' && req.url().endsWith('/graphql') && (req.postData() || '').includes('createCommitOnBranch');

const tab = async (ctx) => {
  const page = await ctx.newPage();
  const t = { page, mutations: [], answers: [], requests: [], console: [] };
  page.on('console', (m) => t.console.push(m.text()));
  page.on('request', (req) => {
    if (!req.url().startsWith('https://api.github.com/')) return;
    t.requests.push({ url: req.url(), method: req.method(), body: req.postData() || '' });
    if (isMutation(req)) t.mutations.push(JSON.parse(req.postData()));
  });
  return t;
};

const saveState = (page) => page.getByRole('status', { name: /save state/i });

const connect = async (t, p, branch) => {
  await t.page.goto(`${env.app}#/investigations`);
  await expect(t.page.getByRole('button', { name: /connect/i }).first()).toBeVisible({ timeout: 30000 });
  if (!(await t.page.getByLabel('Repository owner', { exact: true }).isVisible().catch(() => false))) await t.page.getByRole('button', { name: /connect/i }).first().click();
  await t.page.getByLabel('Repository owner', { exact: true }).fill(p.owner);
  await t.page.getByLabel('Repository', { exact: true }).fill(p.repo);
  await t.page.getByLabel('Branch', { exact: true }).fill(branch);
  await t.page.getByLabel('Token', { exact: true }).fill(p.token);
  await t.page.getByRole('button', { name: /connect/i }).first().click();
  await expect(t.page.getByText(`Connected to ${p.owner}/${p.repo} on branch ${branch}`).first()).toBeVisible({ timeout: 30000 });
};

const marker = (label) => `${label}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;

const start = async (t, label) => {
  const v = { problem: marker(`${label}-problem`), title: marker(`${label}-title`), question: marker(`${label}-question`) };
  await t.page.getByRole('link', { name: /new investigation/i }).or(t.page.getByRole('button', { name: /new investigation/i })).first().click();
  await t.page.getByLabel('Problem statement', { exact: true }).fill(v.problem);
  await t.page.getByLabel('Title', { exact: true }).fill(v.title);
  await t.page.getByLabel('Primary investigative question', { exact: true }).fill(v.question);
  await t.page.getByRole('button', { name: 'Start investigation', exact: true }).click();
  await expect(saveState(t.page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 20000 });
  v.id = t.page.url().match(/\/([0-9a-f-]{36})\//)[1];
  return v;
};
const observe = async (t, text) => {
  await t.page.getByRole('navigation', { name: /investigation sections/i }).getByRole('link', { name: 'Overview', exact: true }).click();
  await t.page.getByLabel('Observation text', { exact: true }).fill(text);
  await t.page.getByLabel('Observation source reference', { exact: true }).fill(`${text}-source`);
  await t.page.getByRole('button', { name: 'Record an observation', exact: true }).click();
  await expect(t.page.getByText(text).first()).toBeVisible();
};
const saveButton = (page) => page.getByRole('button', { name: 'Save', exact: true });
const buttons = async (page) => (await page.getByRole('button').allInnerTexts()).map((s) => s.trim());
const details = (page) => page.locator('main').innerText();

/// causa-data's active catalog and the lightbulb case, at the pinned commit (or from CAUSA_CATALOG_SOURCE, a local copy).
const PINNED = 'e8259df74add723765c57a5b1a5888d9c3f707af';
const LIGHTBULB = 'workspaces/demo/demonstration-v1/investigations/fc3f71a9-4c14-5a31-8196-7ce17a7041c1';
const catalogFile = async (path) => (process.env.CAUSA_CATALOG_SOURCE
  ? readFileSync(`${process.env.CAUSA_CATALOG_SOURCE}/${path}`)
  : Buffer.from(await (await fetch(`https://raw.githubusercontent.com/kemiller2002/causa-data/${PINNED}/${path}`)).arrayBuffer()));
const seedCatalog = async (p, branch) => {
  const paths = ['catalog/active.json', 'catalog/demonstration-v1/manifest.json', `${LIGHTBULB}/manifest.json`, `${LIGHTBULB}/events.ndjson`, `${LIGHTBULB}/snapshot.json`];
  const additions = [];
  for (const path of paths) additions.push({ path, contents: (await catalogFile(path)).toString('base64') });
  const head = await headOf(p, branch);
  const res = await api(p, '/graphql', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ query: 'mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid } } }', variables: { input: { branch: { repositoryNameWithOwner: `${p.owner}/${p.repo}`, branchName: branch }, expectedHeadOid: head, message: { headline: `Causa: causa-data catalog at ${PINNED}` }, fileChanges: { additions, deletions: [] } } } }) });
  const oid = res.body?.data?.createCommitOnBranch?.commit?.oid;
  if (!oid) throw new Error(`cannot seed the catalog: ${JSON.stringify(res.body).slice(0, 300)}`);
  return oid;
};

const provider = () => {
  if (process.env.CAUSA_PROVIDER === 'harness-double') {
    if (!env.double) throw new Error('CAUSA_PROVIDER=harness-double needs CAUSA_DOUBLE_URL');
    return providers.fake();
  }
  if (!env.token) throw new Error(BLOCKED);
  return providers.live();
};

const SEVEN = ['Saved', 'Unsaved', 'Saving', 'Offline', 'Conflict', 'Unknown commit outcome', 'Save failed'];

/// Walks one investigation through every state, recording the indicator's word, its Forma lozenge and the actions.
const tour = async (browser) => {
  const p = provider();
  const branch = await freshBranch(p, 'ui006');
  let policy = () => 'pass';
  const ctx = await contextFor(browser, p, (req) => policy(req));
  const t = await tab(ctx);
  const seen = [];
  const record = async (expected) => {
    await expect(saveState(t.page)).toHaveText(new RegExp(`^\\s*${expected}\\s*$`), { timeout: 60000 });
    const lozenge = await saveState(t.page).evaluate((e) => ({ cls: e.className, state: e.dataset.state, role: e.getAttribute('role'), label: e.getAttribute('aria-label') }));
    seen.push({ word: expected, lozenge, buttons: await buttons(t.page), text: await details(t.page) });
  };
  await connect(t, p, branch);
  const v = await start(t, 'ui006');
  await record('Unsaved');
  // Saving: the commit's answer is held; Saved must not show before GitHub answered and the read-back matched.
  policy = (req) => (isMutation(req) ? 'hold' : 'pass');
  await saveButton(t.page).click();
  await record('Saving');
  await expect.poll(() => ctx.heldCount()).toBe(1);
  const heldState = (await saveState(t.page).innerText()).trim();
  policy = () => 'pass';
  ctx.release();
  await record('Saved');
  const committed = await headOf(p, branch);
  const readBackSeen = t.requests.some((q) => q.body.includes(committed) || q.url.includes(committed));
  const lastSaved = await t.page.locator('time[data-text="investigation.save.lastSaved"]').getAttribute('datetime').catch(() => null);
  const commitDate = (await api(p, repoPath(p, `/commits/${committed}`))).body?.commit?.committer?.date;
  // Offline.
  await observe(t, marker('offline-change'));
  await ctx.setOffline(true);
  await record('Offline');
  await ctx.setOffline(false);
  await record('Unsaved');
  // Unknown commit outcome: the answer is lost after GitHub applied the commit; reconciliation later confirms it.
  policy = (req) => (isMutation(req) ? 'lose' : 'pass');
  await saveButton(t.page).click();
  await record('Unknown commit outcome');
  const mutationsWhileUnknown = t.mutations.length;
  policy = () => 'pass';
  await record('Saved');
  // Save failed: GitHub refuses the commit.
  await observe(t, marker('refused-change'));
  policy = (req) => (isMutation(req) ? 'refuse' : 'pass');
  await saveButton(t.page).click();
  await record('Save failed');
  policy = () => 'pass';
  await saveButton(t.page).click();
  await record('Saved');
  // Conflict: another tab saves this investigation first; this tab's change stays here.
  const other = await tab(ctx);
  await connect(other, p, branch);
  await other.page.getByRole('link', { name: v.title }).first().click();
  const theirs = marker('their-change');
  await observe(other, theirs);
  await saveButton(other.page).click();
  await expect(saveState(other.page)).toHaveText(/^\s*Saved\s*$/, { timeout: 60000 });
  const mine = marker('my-change');
  await observe(t, mine);
  await saveButton(t.page).click();
  await record('Conflict');
  const mutationsAtConflict = t.mutations.length;
  await t.page.waitForTimeout(3000);
  const conflictHeld = { state: (await saveState(t.page).innerText()).trim(), mutations: t.mutations.length, text: await details(t.page) };
  const remoteHead = await headOf(p, branch);
  const remote = await (async () => {
    const tree = (await api(p, repoPath(p, `/git/trees/${remoteHead}?recursive=1`))).body?.tree || [];
    const file = tree.find((e) => e.path.endsWith(`${v.id}/events.ndjson`));
    if (!file) return '';
    const blob = (await api(p, repoPath(p, `/git/blobs/${file.sha}`))).body;
    return Buffer.from(blob.content, 'base64').toString('utf8');
  })();
  // Frozen fixtures: the catalog investigation is read-only.
  await seedCatalog(p, branch);
  const reader = await tab(ctx);
  await connect(reader, p, branch);
  await reader.page.getByRole('link', { name: /Burned-out ceiling lightbulb/ }).first().click();
  await expect(reader.page.getByText('Frozen demonstration (read-only)').first()).toBeVisible({ timeout: 60000 });
  const catalog = { buttons: await buttons(reader.page), state: (await saveState(reader.page).innerText().catch(() => '')).trim(), text: await details(reader.page), mutations: reader.mutations.length };
  // Tokens: everything the pages expose.
  const exposed = [];
  for (const page of [t.page, other.page, reader.page]) {
    exposed.push(page.url(), await page.content(), JSON.stringify(await page.evaluate(() => ({ local: { ...localStorage }, session: { ...sessionStorage } }))));
  }
  exposed.push(...t.console, ...other.console, ...reader.console, ...t.requests.map((q) => q.url), ...other.requests.map((q) => q.url), ...reader.requests.map((q) => q.url));
  await ctx.close();
  return { p, v, seen, heldState, readBackSeen, committed, lastSaved, commitDate, mutationsWhileUnknown, mutationsAtConflict, conflictHeld, remote, theirs, mine, catalog, exposed };
};

let cached = null;
let failed = null;
const result = async (browser) => {
  if (cached) return cached;
  if (failed) throw failed;
  try { cached = await tour(browser); return cached; } catch (e) { failed = e; throw e; }
};

test.describe.configure({ timeout: 400000 });

test('UI-006-O1', async ({ browser }) => {
  const r = await result(browser);
  // Exactly seven states, each shown in the one accessible indicator, a Forma status lozenge.
  expect(new Set(r.seen.map((s) => s.word))).toEqual(new Set(SEVEN));
  for (const s of r.seen) {
    expect(SEVEN).toContain(s.word);
    expect(s.lozenge.cls).toContain('ef-status-lozenge');
    expect(s.lozenge.role).toBe('status');
    expect(s.lozenge.label).toBe('Save state');
  }
});

test('UI-006-O2', async ({ browser }) => {
  const r = await result(browser);
  const at = (word) => r.seen.filter((s) => s.word === word);
  // PERSIST-031: Saved and Saving offer no save; Unsaved offers Save; Offline offers no save until the network returns;
  // Unknown commit outcome sends nothing more until reconciliation; Save failed names the cause, keeps the changes and
  // offers Save again; Conflict offers its explicit resolutions.
  for (const s of at('Saved')) expect(s.buttons).not.toContain('Save');
  for (const s of at('Saving')) expect(s.buttons).not.toContain('Save');
  for (const s of at('Unsaved')) expect(s.buttons).toContain('Save');
  for (const s of at('Offline')) expect(s.buttons).not.toContain('Save');
  for (const s of at('Unknown commit outcome')) {
    expect(s.buttons).not.toContain('Save');
    expect(s.text).toMatch(/may or may not have reached GitHub/);
  }
  for (const s of at('Save failed')) {
    expect(s.buttons).toContain('Save');
    expect(s.text).toMatch(/kept in this tab/);
  }
  for (const s of at('Conflict')) for (const action of ['Reapply mine', 'Discard mine', 'Export mine', 'Read again']) expect(s.buttons).toContain(action);
  expect(r.mutationsWhileUnknown).toBeGreaterThan(0);
});

test('UI-006-O3', async ({ browser }) => {
  const r = await result(browser);
  // While GitHub's answer was held the indicator said Saving, never Saved; Saved came after a read of the commit.
  expect(r.heldState).toBe('Saving');
  expect(r.readBackSeen).toBe(true);
});

test('UI-006-O4', async ({ browser }) => {
  const r = await result(browser);
  expect(r.lastSaved).toBeTruthy();
  expect(r.commitDate).toBeTruthy();
  expect(Math.abs(Date.parse(r.lastSaved) - Date.parse(r.commitDate))).toBeLessThan(1000);
});

test('UI-006-O5', async ({ browser }) => {
  const r = await result(browser);
  // The conflict keeps this tab's change and waits for an explicit resolution: no further commit is sent.
  expect(r.conflictHeld.state).toBe('Conflict');
  expect(r.conflictHeld.mutations).toBe(r.mutationsAtConflict);
  expect(r.conflictHeld.text).toContain(r.mine);
  // GitHub's history is not overwritten: it holds the other writer's change and not this tab's.
  expect(r.remote).toContain(r.theirs);
  expect(r.remote).not.toContain(r.mine);
});

test('UI-006-O6', async ({ browser }) => {
  const r = await result(browser);
  expect(r.catalog.text).toContain('Frozen demonstration (read-only)');
  expect(r.catalog.buttons).not.toContain('Save');
  expect(r.catalog.state).not.toBe('Unsaved');
  expect(r.catalog.mutations).toBe(0);
});

test('UI-006-O7', async ({ browser }) => {
  const r = await result(browser);
  expect(r.p.token.length).toBeGreaterThan(0);
  for (const text of r.exposed) expect(text.includes(r.p.token)).toBe(false);
});
