// Real-browser checks for the application pages.
//
// The .NET tests prove the engine decides correctly; they cannot prove a page
// loads. These tests serve the repository root (the pages reach into
// node_modules/ for Forma and Folio) and drive the pages in Chromium.
import { existsSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

const port = 4321;
const origin = `http://127.0.0.1:${port}`;

// Some environments ship a Chromium that Playwright did not download itself.
// Where that binary exists it is used as-is; everywhere else Playwright
// resolves its own, so CI needs no special case.
const preinstalledChromium = "/opt/pw-browsers/chromium";
const launchOptions = existsSync(preinstalledChromium) ? { executablePath: preinstalledChromium } : {};

export default defineConfig({
  testDir: "./tests/browser",
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 60_000,
  reporter: process.env.CI ? [["github"], ["list"]] : [["list"]],
  use: { baseURL: origin, trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"], launchOptions } }],
  webServer: {
    command: `python3 -m http.server ${port} --bind 127.0.0.1`,
    url: `${origin}/src/kernel/index.html`,
    reuseExistingServer: !process.env.CI,
    timeout: 60_000
  }
});
