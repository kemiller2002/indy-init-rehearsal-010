// Owner: ui. UI-003: the Timeline section (Views/Workspace/Timeline.fs). Needs an investigation with content.
import { test, expect } from '@playwright/test';
import { investigationWithContent, section } from './workspace-data.mjs';

const events = (page) => page.getByRole('list', { name: 'Investigation events' }).getByRole('listitem');

test('UI-003-O1', async ({ page }) => {
  // Every event in chronological order with its type, actor, time and version.
  await investigationWithContent(page);
  await section(page, 'Timeline');
  const first = events(page).first();
  await expect(first).toContainText('investigation started');
  await expect(first).toContainText(/version 1\b/);
  await expect(first.locator('time')).toHaveAttribute('datetime', /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/);
  const versions = (await events(page).allTextContents()).map((t) => Number(t.match(/version (\d+)/)[1]));
  expect(versions).toEqual([...versions].sort((a, b) => a - b));
});

test('UI-003-O2', async ({ page }) => {
  // Filtering by category, with the number shown.
  await investigationWithContent(page);
  await section(page, 'Timeline');
  const all = await events(page).count();
  await page.getByRole('combobox', { name: 'Show events' }).selectOption({ label: 'Assessments' });
  await expect(events(page)).toHaveCount(2);
  await expect(page.getByText(`2 of ${all} events`)).toBeVisible();
  for (const t of await events(page).allTextContents()) expect(t).toContain('Assessments');
});

test('UI-003-O3', async ({ page }) => {
  // History is ordered by version and says what its times are; nothing on the timeline edits it.
  await investigationWithContent(page);
  await section(page, 'Timeline');
  await expect(page.getByText('Events are listed in the order they were recorded (by version). Times are the times each event occurred.')).toBeVisible();
  await expect(events(page).first()).toBeVisible();
  await expect(page.getByRole('list', { name: 'Investigation events' }).getByRole('button')).toHaveCount(0);
  await expect(page.getByRole('list', { name: 'Investigation events' }).getByRole('textbox')).toHaveCount(0);
});

test('UI-003-O4', async ({ page }) => {
  // An event links to the record it affected.
  const v = await investigationWithContent(page);
  await section(page, 'Timeline');
  await events(page).filter({ hasText: `Hypothesis recorded: ${v.h1}` }).getByRole('link', { name: 'Open the hypothesis' }).click();
  await expect(page.getByRole('complementary').filter({ hasText: v.h1 })).toBeVisible();
  expect(page.url()).toMatch(/\/hypotheses\?record=/);
});
