// Owner: integration. Written at bootstrap by requirements-compiler v41 (package 4.1.5).
// Defines every Playwright project the verification execution plan's runners name; a runner that names a
// project this file does not define cannot execute (r9.fsx suite-preflight and validation check X21).
import { existsSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

// CAUSA_TEST_PORT lets several worktrees on one machine each serve their own build (a reused server would be
// another worktree's); an existing server is reused only when CAUSA_REUSE_SERVER is set.
const port = Number(process.env.CAUSA_TEST_PORT || 4321);
const origin = `http://127.0.0.1:${port}`;
// The offline browser suites (SU-BROWSER-CHROMIUM, SU-BROWSER-WEBKIT) run "the test build served once per worker with
// the stateful fake" (verification-execution-plan.json): unless CAUSA_PROVIDER=github, the stateful GitHub fake
// (tests/Causa.FakeGitHub, over tests/Causa.TestSupport/StatefulFake.fs) is started here and named in CAUSA_DOUBLE_URL,
// which the specs read (tests/browser/support/app.mjs). CAUSA_DOUBLE_URL set by the caller names a fake already running
// (nothing is started); CAUSA_DOUBLE_PORT moves the started one (default 4610). Its repository and credential are the
// specs' harness defaults (CAUSA_TEST_OWNER, CAUSA_TEST_REPO, CAUSA_TEST_BRANCH; the fake reads CAUSA_GITHUB_TOKEN
// from its environment, never from its command line).
const harness = process.env.CAUSA_PROVIDER !== "github";
const startFake = harness && !process.env.CAUSA_DOUBLE_URL;
const fakePort = Number(process.env.CAUSA_DOUBLE_PORT || 4610);
if (startFake) process.env.CAUSA_DOUBLE_URL = `http://127.0.0.1:${fakePort}`;
const fake = {
  owner: process.env.CAUSA_TEST_OWNER || "harness",
  repo: process.env.CAUSA_TEST_REPO || "causa-test",
  branch: process.env.CAUSA_TEST_BRANCH || "main",
};
const quoted = (v: string) => `'${v.replace(/'/g, "'\\''")}'`;
const fakeServer = {
  command: `dotnet run --project tests/Causa.FakeGitHub -c Release -- --port ${fakePort} --owner ${quoted(fake.owner)} --repo ${quoted(fake.repo)} --branch ${quoted(fake.branch)}`,
  // Any HTTP answer means it is listening (an unauthenticated read is refused with 401, which counts as ready).
  url: `http://127.0.0.1:${fakePort}/repos/${fake.owner}/${fake.repo}`,
  reuseExistingServer: false,
  timeout: 300_000,
};
const preinstalledChromium = existsSync("/opt/pw-browsers/chromium") ? { executablePath: "/opt/pw-browsers/chromium" } : {};

export default defineConfig({
  testDir: "./tests/browser",
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  use: { baseURL: origin, trace: "retain-on-failure" },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"], launchOptions: preinstalledChromium } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } },
  ],
  // `npm run build` writes the application to dist/ (served at /dist/); the repository root stays served for the
  // scaffold pages. Set CAUSA_APP_URL to run the application specs against a deployed build instead.
  // Started in order: the application build first, then the fake (whose build shares the application's projects).
  webServer: [
    { command: `npm run build && python3 -m http.server ${port} --bind 127.0.0.1 2> /dev/null`, url: origin, reuseExistingServer: !process.env.CI && !!process.env.CAUSA_REUSE_SERVER, timeout: 300_000 },
    ...(startFake ? [fakeServer] : []),
  ]
});
