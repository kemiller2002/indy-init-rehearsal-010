// Owner: integration. Opening the built application in the browser specs: the served build (dist/, at /dist/ under
// playwright.config.ts) or CAUSA_APP_URL. Waits for the engine's first view, never for a fixed time.
import { expect } from '@playwright/test';

export const appUrl = (route = '#/investigations') => `${process.env.CAUSA_APP_URL || '/dist/'}${route}`;

/// The engine has started: the loading status is gone and the engine wrote the document title.
export const started = async (page) => {
  await expect(page.getByRole('status').filter({ hasText: 'Loading Causa…' })).toBeHidden({ timeout: 30000 });
  await expect(page).toHaveTitle(/· Causa$/);
};

/// A fresh page load at a route (a goto that differs only in its hash would not reload the application).
export const load = async (page, route, options = {}) => {
  if (page.url() !== 'about:blank') await page.goto('about:blank');
  await page.goto(appUrl(route), options);
};

export const open = async (page, route) => {
  await load(page, route);
  await started(page);
};

/// The primary navigation shown at this viewport (the persistent column, or the compact menu, opened).
export const primary = async (page) => {
  const persistent = page.locator('nav.ef-navigation-shell__persistent');
  if (await persistent.isVisible()) return persistent;
  const menu = page.locator('details.ef-navigation-shell__compact');
  if (!(await menu.evaluate((d) => d.open))) await menu.locator('summary').click();
  return menu.locator('nav');
};

export const viewports = [
  { id: 'mobile', width: 390, height: 844 },
  { id: 'ipad-portrait', width: 820, height: 1180 },
  { id: 'ipad-landscape', width: 1180, height: 820 },
  { id: 'desktop', width: 1440, height: 900 },
];

export const destinations = ['Investigations', 'Administration', 'Settings'];
export const routes = { Investigations: '#/investigations', Administration: '#/administration', Settings: '#/settings' };

/// A catalog investigation route and a working-copy one (the route states the workspace; nothing is read).
export const catalogInvestigation = '#/investigation/workspaces%2Fdemo%2Fhistorical-v1/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview';
export const workingCopyInvestigation = '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview';

/// Every computed colour the page paints (text, background, borders, outlines, SVG fills and strokes).
export const paintedColours = (page) => page.evaluate(() => {
  const seen = new Set();
  for (const el of document.querySelectorAll('body, body *')) {
    // Only what is rendered: hidden fallbacks (the load failure, closed menus) paint nothing.
    if (!el.getClientRects().length) continue;
    const s = getComputedStyle(el);
    if (s.visibility === 'hidden') continue;
    for (const c of [s.color, s.backgroundColor, s.borderTopColor, s.borderRightColor, s.borderBottomColor, s.borderLeftColor, s.outlineColor, s.fill, s.stroke]) if (c) seen.add(c.replace(/\s/g, ''));
  }
  return [...seen];
});
export const rgb = (hex) => `rgb(${[1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)).join(',')})`;
export const palette = { navy: '#14283F', teal: '#087F8C', amber: '#E9A23B', coral: '#D66B58', ivory: '#F5F3ED' };

// ---- Provider guard: a spec that needs a provider fails at once, as BLOCKED, when none is configured --------------
// A BLOCKED test is a failed test (never skipped, never answered by a substitute); the guard only makes it fail before
// the browser waits on a connection that cannot come, so a run without a provider ends in seconds, not at the job limit.

/// The real GitHub provider: a token and the designated test repository (the governed journeys' variables).
export const requireGitHub = (purpose = 'this test needs the real GitHub provider') => {
  const missing = ['CAUSA_GITHUB_TOKEN', 'CAUSA_TEST_OWNER', 'CAUSA_TEST_REPO'].filter((name) => !process.env[name]);
  if (missing.length) throw new Error(`BLOCKED: ${missing.join(', ')} ${missing.length === 1 ? 'is' : 'are'} not set; ${purpose} (a GitHub token and the designated test repository).`);
};

const doubleAnswers = new Map();
/// The harness GitHub double answers at this URL (any HTTP answer counts; a refused or silent connection does not).
const answers = (url) => {
  if (!doubleAnswers.has(url)) {
    doubleAnswers.set(url, fetch(url, { signal: AbortSignal.timeout(3000) }).then(() => true, () => false));
  }
  return doubleAnswers.get(url);
};

/// The provider a spec's documented modes name: CAUSA_PROVIDER=github needs the real provider; otherwise the harness
/// double (CAUSA_DOUBLE_URL, default http://127.0.0.1:4610) must be answering.
export const requireProvider = async ({ provider, double }, purpose = 'this test needs a connected provider') => {
  if (provider === 'github') return requireGitHub(purpose);
  if (!(await answers(double))) {
    throw new Error(`BLOCKED: the harness GitHub double does not answer at ${double}; ${purpose}. Start the double and set CAUSA_DOUBLE_URL, or set CAUSA_PROVIDER=github with CAUSA_GITHUB_TOKEN, CAUSA_TEST_OWNER and CAUSA_TEST_REPO.`);
  }
};

// ---- The provider the browser specs connect to ------------------------------------------------------------------------
// Offline (SU-BROWSER-CHROMIUM, SU-BROWSER-WEBKIT) the provider is the stateful GitHub fake served by
// tests/Causa.FakeGitHub, which playwright.config.ts starts and names in CAUSA_DOUBLE_URL ("the test build served once
// per worker with the stateful fake", verification-execution-plan.json). CAUSA_PROVIDER=github connects to the
// designated test repository instead (CAUSA_GITHUB_TOKEN, CAUSA_TEST_OWNER, CAUSA_TEST_REPO). Fake answers are never
// live evidence (PERSIST-025-O4, UI-015-O3).

/// The provider, repository and credential every provider-dependent spec connects with. The harness defaults are the
/// repository and credential the fake is started with (playwright.config.ts).
export const providerEnv = Object.freeze({
  provider: process.env.CAUSA_PROVIDER === 'github' ? 'github' : 'harness-double',
  token: process.env.CAUSA_GITHUB_TOKEN || 'harness-token',
  owner: process.env.CAUSA_TEST_OWNER || 'harness',
  repo: process.env.CAUSA_TEST_REPO || 'causa-test',
  branch: process.env.CAUSA_TEST_BRANCH || 'main',
  double: process.env.CAUSA_DOUBLE_URL || 'http://127.0.0.1:4610',
});

/// Where a request to api.github.com is answered: the request's own URL with the real provider, the fake otherwise.
export const providerUrl = (url, env = providerEnv) => {
  if (env.provider === 'github') return url;
  const u = new URL(url);
  return env.double + u.pathname + u.search;
};

/// Sends an intercepted GitHub request on to the provider and returns its whole answer (never fulfilled here). Use it
/// instead of route.fetch(), which goes to the network and would bypass the fake.
export const forward = (route, env = providerEnv) => route.fetch({ url: providerUrl(route.request().url(), env) });

const routedContexts = new WeakSet();
/// In harness mode, every api.github.com request of this browser context is answered by the fake (once per context; a
/// page route a spec adds later still sees the request first and may fall back to this one). The answer is read whole
/// before it is fulfilled, so a page that moved on cannot dispose it half-way.
export const routeToProvider = async (context, env = providerEnv) => {
  if (env.provider === 'github' || routedContexts.has(context)) return;
  routedContexts.add(context);
  await context.route('https://api.github.com/**', async (route) => {
    let answer;
    try {
      const r = await forward(route, env);
      answer = { status: r.status(), headers: r.headers(), body: await r.body() };
    } catch {
      await route.abort().catch(() => {});
      return;
    }
    await route.fulfill(answer).catch(() => {});
  });
};

/// Fails at once as BLOCKED when the provider is missing, then routes the page's context to it.
export const useProvider = async (page, purpose, env = providerEnv) => {
  await requireProvider(env, purpose);
  await routeToProvider(page.context(), env);
};

/// Fills the connection form by its exact labels (integration contract: "Repository owner", "Repository", "Branch",
/// "Token") and submits it with its one submit button, "Connect data repository". Loose patterns are not used: /branch/i,
/// /token|credential/i and /connect/i also name other controls (the branch and token descriptions, "Disconnect").
export const submitConnection = async (page, env = providerEnv) => {
  const exact = (name) => page.getByLabel(name, { exact: true });
  await exact('Repository owner').fill(env.owner);
  await exact('Repository').fill(env.repo);
  await exact('Branch').fill(env.branch);
  await exact('Token').fill(env.token);
  await page.getByRole('button', { name: 'Connect data repository', exact: true }).click();
};
