// Owner: ui. UI-015: the UI acceptance suite itself is traceable and truthful.
import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync } from 'node:fs';
import { open } from '../support/app.mjs';

const here = new URL('.', import.meta.url);
const specs = readdirSync(here).filter((f) => /^UI-\d{3}\.spec\.js$/.test(f));

test('UI-015-O1', async () => {
  // Every UI acceptance spec is tagged with its requirement and every test with one of that requirement's obligations.
  expect(specs.length).toBeGreaterThan(20);
  for (const f of specs) {
    const id = f.replace('.spec.js', '');
    const text = readFileSync(new URL(f, here), 'utf8');
    expect(text, f).toContain(`// Owner: ui. ${id}`);
    const tests = [...text.matchAll(/test\('([^']+)'/g)].map((m) => m[1]);
    expect(tests.length, f).toBeGreaterThan(0);
    for (const t of tests) expect(t, f).toMatch(new RegExp(`^${id}-O\\d+$`));
  }
});

test('UI-015-O2', async () => {
  // The suite covers the named concerns: graph text equivalent, timeline order, engine-authorized commands, save/sync
  // truthfulness copy, offline, responsive reflow and startup failure each have a spec.
  for (const f of ['UI-026.spec.js', 'UI-003.spec.js', 'UI-004.spec.js', 'UI-010.spec.js', 'UI-011.spec.js', 'UI-016.spec.js']) expect(specs, f).toContain(f);
});

test('UI-015-O3', async () => {
  // Data-backed specs say which provider they use; the harness double is never presented as live write verification.
  const helper = readFileSync(new URL('workspace-data.mjs', here), 'utf8');
  expect(helper).toContain("process.env.CAUSA_PROVIDER || 'harness-double'");
  expect(helper).toContain('nothing here is live-write evidence');
});

test('UI-015-O4', async ({ page }) => {
  // What is not verified is shown as unknown or unavailable, never as passed.
  await open(page, '#/administration');
  const facts = page.getByRole('definition');
  const text = (await facts.allTextContents()).join(' | ');
  expect(text).not.toMatch(/\bPASS\b|\bpassed\b/i);
  await expect(page.locator('.causa-fact').filter({ hasText: 'Deployed commit' })).toContainText(/Unavailable|Unknown|Known/);
  await expect(page.locator('.causa-fact').filter({ hasText: 'Data validation' })).toContainText('Unknown');
});
