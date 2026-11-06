// Owner: integration. Written by the persistence lane (approved by the integration authority).
// PERSIST-049 (live browser): changes saved from several tabs, the tabs closed, the investigation opened in a new tab
// and a new browser context: Saved at the latest commit, every confirmed change, nothing unconfirmed; a tab reloaded
// with unsaved changes shows only GitHub's confirmed state. Against the GitHub test repository (CAUSA_GITHUB_TOKEN;
// BLOCKED without it) and the stateful fake (CAUSA_DOUBLE_URL).
import { test, expect } from '@playwright/test';

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

/// A browser context whose GitHub requests go to the provider (the fake is reached by rewriting the origin only).
const contextFor = async (browser, p) => {
  const ctx = await browser.newContext();
  if (p.mode === 'fake') {
    await ctx.route('https://api.github.com/**', async (route) => {
      const u = new URL(route.request().url());
      const answer = await route.fetch({ url: p.base + u.pathname + u.search }).catch(() => null);
      if (answer) await route.fulfill({ response: answer }); else await route.abort();
    });
  }
  return ctx;
};

/// One tab: the page, the save mutations it sent and GitHub's answers to them, and every request it made.
const tab = async (ctx) => {
  const page = await ctx.newPage();
  const t = { page, mutations: [], answers: [], requests: [] };
  page.on('request', (req) => {
    if (!req.url().startsWith('https://api.github.com/')) return;
    t.requests.push({ url: req.url(), method: req.method(), body: req.postData() || '' });
    if (req.method() === 'POST' && req.url().endsWith('/graphql') && (req.postData() || '').includes('createCommitOnBranch')) t.mutations.push(JSON.parse(req.postData()));
  });
  page.on('response', async (res) => {
    const req = res.request();
    if (req.method() === 'POST' && req.url().endsWith('/graphql') && (req.postData() || '').includes('createCommitOnBranch')) {
      const body = await res.json().catch(() => null);
      t.answers.push(body?.data?.createCommitOnBranch?.commit?.oid || null);
    }
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
  const m = t.page.url().match(/#\/investigation\/([^/]+)\/([0-9a-f-]{36})\//);
  v.workspace = decodeURIComponent(m[1]);
  v.id = m[2];
  v.prefix = `${v.workspace}/investigations/${v.id}/`;
  return v;
};


const observe = async (t, text) => {
  await t.page.getByRole('navigation', { name: /investigation sections/i }).getByRole('link', { name: 'Overview', exact: true }).click();
  await t.page.getByLabel('Observation text', { exact: true }).fill(text);
  await t.page.getByLabel('Observation source reference', { exact: true }).fill(`${text}-source`);
  await t.page.getByRole('button', { name: 'Record an observation', exact: true }).click();
  await expect(t.page.getByText(text).first()).toBeVisible();
};
const save = async (t) => {
  await t.page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(saveState(t.page)).toHaveText(/^\s*Saved\s*$/, { timeout: 90000 });
};
/// Connects a tab and opens the investigation from the working-copy list, as a user does.
const reopen = async (t, p, branch, v) => {
  await connect(t, p, branch);
  await t.page.getByRole('link', { name: v.title }).first().click();
  await expect(t.page.getByText(v.problem).first()).toBeVisible({ timeout: 30000 });
};

/// The scenario on one provider.
const scenario049 = async (browser, p) => {
  const branch = await freshBranch(p, 'persist049');
  const ctx = await contextFor(browser, p);
  // Tab A creates and saves the investigation and a first observation.
  const a = await tab(ctx);
  await connect(a, p, branch);
  const v = await start(a, 'persist049');
  await save(a);
  const obsA = marker('obs-a');
  await observe(a, obsA);
  await save(a);
  // Tab B opens it, records and saves a second observation.
  const b = await tab(ctx);
  await reopen(b, p, branch, v);
  const obsB = marker('obs-b');
  await observe(b, obsB);
  await save(b);
  // Tab C records a change it never saves.
  const c = await tab(ctx);
  await reopen(c, p, branch, v);
  const obsC = marker('obs-c-unconfirmed');
  await observe(c, obsC);
  await expect(saveState(c.page)).toHaveText(/^\s*Unsaved\s*$/);
  const confirmed = [...a.answers, ...b.answers].filter(Boolean).at(-1);
  for (const t of [a, b, c]) await t.page.close();
  const head = await headOf(p, branch);
  // A new tab in the same context, and a new browser context.
  const inTab = await tab(ctx);
  await reopen(inTab, p, branch, v);
  const ctx2 = await contextFor(browser, p);
  const inContext = await tab(ctx2);
  await reopen(inContext, p, branch, v);
  const views = [];
  for (const t of [inTab, inContext]) {
    await t.page.getByRole('navigation', { name: /investigation sections/i }).getByRole('link', { name: 'Overview', exact: true }).click();
    await expect(t.page.getByText(obsB).first()).toBeVisible({ timeout: 30000 });
    views.push({ state: (await saveState(t.page).innerText()).trim(), text: await t.page.locator('main').innerText(), requests: t.requests });
  }
  // A tab with an unsaved change is reloaded: it shows only the confirmed GitHub state.
  const d = inTab;
  const obsD = marker('obs-d-unsaved');
  await observe(d, obsD);
  await expect(saveState(d.page)).toHaveText(/^\s*Unsaved\s*$/);
  await d.page.reload();
  await reopen(d, p, branch, v);
  await d.page.getByRole('navigation', { name: /investigation sections/i }).getByRole('link', { name: 'Overview', exact: true }).click();
  await expect(d.page.getByText(obsB).first()).toBeVisible({ timeout: 30000 });
  const reloaded = { state: (await saveState(d.page).innerText()).trim(), text: await d.page.locator('main').innerText() };
  await ctx.close();
  await ctx2.close();
  return { mode: p.mode, branch, head, confirmed, v, obs: { a: obsA, b: obsB, c: obsC, d: obsD }, views, reloaded, writes: [...a.mutations, ...b.mutations] };
};

const run = {};
const errors = {};
const scenario = async (browser, which) => {
  if (run[which]) return run[which];
  if (errors[which]) throw errors[which];
  try {
    if (which === 'live' && !env.token) throw new Error(BLOCKED);
    if (which === 'fake' && !env.double) throw new Error('BLOCKED: CAUSA_DOUBLE_URL names no stateful fake for the fake half of this test (PERSIST-049-O4). This is a failure, not a pass.');
    run[which] = await scenario049(browser, providers[which]());
    return run[which];
  } catch (e) {
    errors[which] = e;
    throw e;
  }
};
const modes = () => (process.env.CAUSA_PROVIDER === 'harness-double' ? ['fake'] : env.double ? ['live', 'fake'] : ['live']);

test.describe.configure({ timeout: 300000 });

test('PERSIST-049-O1', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    // Several tabs saved; all were closed; the investigation opened in a new tab and in a new browser context.
    expect(r.writes.length).toBeGreaterThanOrEqual(3);
    expect(r.views).toHaveLength(2);
  }
});

test('PERSIST-049-O2', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    expect(r.head).toBe(r.confirmed);
    for (const view of r.views) {
      expect(view.state).toBe('Saved');
      expect(view.text).toContain(r.obs.a);
      expect(view.text).toContain(r.obs.b);
      expect(view.text).not.toContain(r.obs.c);
    }
  }
});

test('PERSIST-049-O3', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    expect(r.reloaded.state).toBe('Saved');
    expect(r.reloaded.text).toContain(r.obs.b);
    expect(r.reloaded.text).not.toContain(r.obs.d);
  }
});

test('PERSIST-049-O4', async ({ browser }) => {
  const live = await scenario(browser, 'live');
  const fake = await scenario(browser, 'fake');
  for (const r of [live, fake]) for (const view of r.views) expect(view.state).toBe('Saved');
  if (env.candidate) {
    const info = await fetch(new URL('build-info.json', env.app.startsWith('http') ? env.app : `http://127.0.0.1:4321${env.app}`)).then((x) => x.json());
    expect(info.commit).toBe(env.candidate);
  }
});

test('PERSIST-049-O5', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    // PERSIST-005: after every tab closed, the new tab and context obtained the investigation from GitHub alone (their
    // requests read it). PERSIST-021: they show Saved at the latest commit with every confirmed save.
    for (const view of r.views) {
      expect(view.requests.some((q) => q.method === 'POST' && q.url.endsWith('/graphql') && q.body.includes(r.v.id))).toBe(true);
      expect(view.state).toBe('Saved');
    }
    // PERSIST-007: each save was one createCommitOnBranch whose commit the branch now holds.
    expect(r.writes.every((w) => /^[0-9a-f]{40}$/.test(w.variables.input.expectedHeadOid))).toBe(true);
    // PERSIST-020: a closed tab's unsaved change is gone (no local recovery); a reload discards the unsaved change.
    for (const view of r.views) expect(view.text).not.toContain(r.obs.c);
    expect(r.reloaded.text).not.toContain(r.obs.d);
  }
});
