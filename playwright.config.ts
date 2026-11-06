// Owner: integration. Written at bootstrap by requirements-compiler v41 (package 4.1.5).
// Defines every Playwright project the verification execution plan's runners name; a runner that names a
// project this file does not define cannot execute (r9.fsx suite-preflight and validation check X21).
import { existsSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

const port = 4321;
const origin = `http://127.0.0.1:${port}`;
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
  webServer: { command: `python3 -m http.server ${port} --bind 127.0.0.1`, url: origin, reuseExistingServer: !process.env.CI, timeout: 60_000 }
});
