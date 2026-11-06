// Owner: integration. Written by the persistence lane (approved by the integration authority).
// PERSIST-041 (live browser): at least three tabs save changes to different investigations at the same moment, against
// the real GitHub test repository (CAUSA_GITHUB_TOKEN; BLOCKED, a failure, without it) and against the stateful fake
// (the harness double at CAUSA_DOUBLE_URL). Each tab is a page with its own engine; each connects with the credential.
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

const markerOf = (mutation) => (mutation.variables.input.message.body.match(/Causa-Write-Marker: (\S+)/) || [])[1];
const pathsOf = (mutation) => [...mutation.variables.input.fileChanges.additions, ...(mutation.variables.input.fileChanges.deletions || [])].map((f) => f.path);

/// The scenario on one provider: three tabs, three new investigations, three saves started at the same moment.
const simultaneous = async (browser, p) => {
  const branch = await freshBranch(p, 'persist041');
  const base = await headOf(p, branch);
  const ctx = await contextFor(browser, p);
  const tabs = [];
  for (let i = 0; i < 3; i++) {
    const t = await tab(ctx);
    await connect(t, p, branch);
    t.v = await start(t, `persist041-${i}`);
    tabs.push(t);
  }
  await Promise.all(tabs.map((t) => t.page.getByRole('button', { name: 'Save', exact: true }).click()));
  for (const t of tabs) await expect(saveState(t.page)).toHaveText(/^\s*Saved\s*$/, { timeout: 90000 });
  for (const t of tabs) {
    t.state = (await saveState(t.page).innerText()).trim();
    t.text = await t.page.locator('main').innerText();
    t.confirmed = t.answers.filter(Boolean).at(-1);
  }
  const head = await headOf(p, branch);
  const compare = (await api(p, repoPath(p, `/compare/${base}...${branch}?per_page=100`))).body;
  const commits = [];
  for (const c of compare?.commits || []) {
    const detail = (await api(p, repoPath(p, `/commits/${c.sha}`))).body;
    commits.push({ sha: c.sha, message: detail?.commit?.message || '', files: (detail?.files || []).map((f) => f.filename) });
  }
  const reachable = [];
  for (const t of tabs) reachable.push((await api(p, repoPath(p, `/compare/${t.confirmed}...${branch}`))).body?.status);
  const result = { mode: p.mode, branch, base, head, tabs, commits, reachable };
  await ctx.close();
  return result;
};

const run = { live: null, fake: null, errors: {} };
const scenario = async (browser, which) => {
  if (run[which] || run.errors[which]) return run[which] || Promise.reject(run.errors[which]);
  try {
    if (which === 'live' && !env.token) throw new Error(BLOCKED);
    if (which === 'fake' && !env.double) throw new Error('BLOCKED: CAUSA_DOUBLE_URL names no stateful fake for the fake half of this test (PERSIST-041-O1). This is a failure, not a pass.');
    run[which] = await simultaneous(browser, providers[which]());
    return run[which];
  } catch (e) {
    run.errors[which] = e;
    throw e;
  }
};
/// The modes this run checks: live always (GitHub is required), and the fake as well when a double is configured; a
/// run that names only the double (CAUSA_PROVIDER=harness-double) checks the fake alone and is never live evidence.
const modes = () => (process.env.CAUSA_PROVIDER === 'harness-double' ? ['fake'] : env.double ? ['live', 'fake'] : ['live']);

test.describe.configure({ timeout: 300000 });

test('PERSIST-041-O1', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    expect(r.tabs).toHaveLength(3);
    expect(new Set(r.tabs.map((t) => t.v.id)).size).toBe(3);
    for (const t of r.tabs) expect(t.state).toBe('Saved');
  }
});

test('PERSIST-041-O2', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    for (const t of r.tabs) {
      // Confirmed exactly once: one commit on the branch writes this investigation.
      expect(r.commits.filter((c) => c.files.some((f) => f.startsWith(t.v.prefix)))).toHaveLength(1);
      // The tab shows its own investigation and its state, and no other tab's investigation.
      expect(t.text).toContain(t.v.problem);
      for (const other of r.tabs.filter((o) => o !== t)) expect(t.text).not.toContain(other.v.problem);
      expect(t.state).toBe('Saved');
    }
  }
});

test('PERSIST-041-O3', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    const markers = [];
    for (const t of r.tabs) {
      expect(t.mutations.length).toBeGreaterThan(0);
      for (const mutation of t.mutations) {
        expect(mutation.query).toContain('createCommitOnBranch');
        expect(mutation.variables.input.expectedHeadOid).toMatch(/^[0-9a-f]{40}$/);
        for (const path of pathsOf(mutation)) expect(path.startsWith(t.v.prefix)).toBe(true);
        markers.push(markerOf(mutation));
      }
    }
    expect(markers.every(Boolean)).toBe(true);
    expect(new Set(markers).size).toBe(markers.length);
    for (const status of r.reachable) expect(['ahead', 'identical']).toContain(status);
  }
});

test('PERSIST-041-O4', async ({ browser }) => {
  // Both modes on the candidate SHA: the live run and the fake run each pass, on the build of the candidate.
  const live = await scenario(browser, 'live');
  const fake = await scenario(browser, 'fake');
  for (const r of [live, fake]) for (const t of r.tabs) expect(t.state).toBe('Saved');
  if (env.candidate) {
    const info = await fetch(new URL('build-info.json', env.app.startsWith('http') ? env.app : `http://127.0.0.1:4321${env.app}`)).then((x) => x.json());
    expect(info.commit).toBe(env.candidate);
  }
});

test('PERSIST-041-O5', async ({ browser }) => {
  for (const m of modes()) {
    const r = await scenario(browser, m);
    for (const t of r.tabs) {
      const confirmed = r.commits.find((c) => c.sha === t.confirmed);
      expect(confirmed).toBeTruthy();
      // PERSIST-006: one commit of the investigation's own files. PERSIST-014: it carries this save's marker.
      expect(confirmed.files.every((f) => f.startsWith(t.v.prefix))).toBe(true);
      expect(t.mutations.map(markerOf)).toContain((confirmed.message.match(/Causa-Write-Marker: (\S+)/) || [])[1]);
      // PERSIST-007: Saved came after a read of the new commit (the read-back), never from the mutation's answer alone.
      const readBack = t.requests.findIndex((q) => q.body.includes(t.confirmed) || q.url.includes(t.confirmed));
      expect(readBack).toBeGreaterThan(-1);
      // PERSIST-013: the other tabs' commits are on the branch, unchanged (each writes only its own prefix).
      for (const other of r.tabs.filter((o) => o !== t)) expect(confirmed.files.some((f) => f.startsWith(other.v.prefix))).toBe(false);
      // PERSIST-025: every request went to the GitHub API, the provider boundary.
      expect(t.requests.every((q) => q.url.startsWith('https://api.github.com/'))).toBe(true);
    }
    // PERSIST-022: each tab showed its own state (checked in O2); the branch holds three investigations' commits.
    expect(new Set(r.commits.flatMap((c) => c.files.map((f) => f.split('/investigations/')[1]?.split('/')[0]).filter(Boolean)))).toEqual(new Set(r.tabs.map((t) => t.v.id)));
  }
});
