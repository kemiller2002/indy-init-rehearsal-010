// Owner: ui. UI-010: specific recoverable states that say what happened, what was kept and the next step.
import { test, expect } from '@playwright/test';
import { open, load, navigate } from '../support/app.mjs';
import { connect, env } from './workspace-data.mjs';

const unopened = '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/evidence';

test('UI-010-O1', async ({ page }) => {
  // Distinct states: an investigation that is not open, an unknown page, and offline each have their own copy.
  await open(page, unopened);
  await expect(page.getByRole('status').filter({ hasText: 'This investigation is not open.' })).toBeVisible();
  await open(page, '#/no-such-page');
  await expect(page.getByText(/There is no page at/)).toBeVisible();
  await open(page, '#/investigations');
  await page.context().setOffline(true);
  await page.evaluate(() => window.dispatchEvent(new Event('offline')));
  await expect(page.getByText('You are offline. Changes stay in this tab until the connection returns.')).toBeVisible();
  await page.context().setOffline(false);
});

test('UI-010-O2', async ({ page }) => {
  // Each unavailable section says what happened, what was preserved and a safe next step.
  await open(page, unopened);
  const state = page.getByRole('status').filter({ hasText: 'This investigation is not open.' });
  await expect(state).toContainText('Nothing was changed.');
  await expect(state).toContainText('Go to Investigations and open it from the list.');
});

test('UI-010-O3', async ({ page }) => {
  // No Retry is offered where it would do nothing (an unopened investigation, an unknown page).
  for (const route of [unopened, '#/no-such-page']) {
    await open(page, route);
    await expect(page.getByText(/This investigation is not open\.|There is no page at/).first(), route).toBeVisible();
    await expect(page.getByRole('button', { name: /retry/i }), route).toHaveCount(0);
  }
});

test('UI-010-O4', async ({ page }) => {
  // Fail closed: an investigation that has not been verified shows no partial content and no record forms.
  await open(page, unopened);
  await expect(page.getByRole('status').filter({ hasText: 'This investigation is not open.' })).toBeVisible();
  await expect(page.getByRole('article')).toHaveCount(0);
  await expect(page.getByRole('textbox')).toHaveCount(0);
});

test('UI-010-O5', async ({ page }) => {
  // The token is never rendered or logged.
  const logged = [];
  page.on('console', (m) => logged.push(m.text()));
  await connect(page);
  for (const route of ['#/investigations', '#/administration', '#/settings']) {
    await navigate(page, route);
    expect(await page.content(), route).not.toContain(env.token);
  }
  expect(logged.join('\n')).not.toContain(env.token);
});
