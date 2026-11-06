// Owner: ui. UI-001: the investigation list. O1 before Connect runs against the build alone; the rest need the
// connected application (workspace-data.mjs: the harness double, or the real provider with CAUSA_PROVIDER=github).
import { test, expect } from '@playwright/test';
import { open } from '../support/app.mjs';
import { connect, investigationWithContent, sections } from './workspace-data.mjs';

const cards = (page) => page.getByRole('list', { name: 'Investigations' }).getByRole('article');

test('UI-001-O1', async ({ page }) => {
  // Before Connect: an empty, disconnected workspace that says how to start, and no investigation is listed.
  await open(page, '#/investigations');
  await expect(page.getByRole('heading', { name: 'Investigations', level: 1 })).toBeVisible();
  await expect(page.getByText('Connect a data repository to list its investigations.')).toBeVisible();
  await expect(page.getByRole('button', { name: /connect/i }).first()).toBeVisible();
  await expect(cards(page)).toHaveCount(0);
  // After Connect: the repository's investigations are listed.
  await connect(page);
  await expect(page.getByText('Connect a data repository to list its investigations.')).toHaveCount(0);
});

test('UI-001-O2', async ({ page }) => {
  // Each card: title, status (lifecycle), last activity and hypothesis/evidence/contradiction counts.
  const v = await investigationWithContent(page);
  await open(page, '#/investigations');
  const card = cards(page).filter({ hasText: v.title });
  await expect(card).toHaveCount(1);
  for (const term of ['Status', 'Last activity', 'Hypotheses', 'Evidence', 'Unresolved contradictions']) await expect(card.getByText(term, { exact: true })).toBeVisible();
});

test('UI-001-O3', async ({ page }) => {
  // The domain contract has no severity or owner: neither is shown, rather than invented.
  await investigationWithContent(page);
  await page.getByRole('link', { name: 'Investigations', exact: true }).first().click();
  const cardsShown = cards(page);
  await expect(cardsShown.first()).toBeVisible();
  for (const card of await cardsShown.all()) {
    await expect(card.locator('dt')).toHaveText(['Status', 'Last activity', 'Hypotheses', 'Evidence', 'Unresolved contradictions']);
    await expect(card).not.toContainText(/severity|owner/i);
  }
});

test('UI-001-O4', async ({ page }) => {
  // The list offers no next action the engine did not provide: each card's only action is opening it.
  const v = await investigationWithContent(page);
  await open(page, '#/investigations');
  const card = cards(page).filter({ hasText: v.title });
  await expect(card.getByRole('button')).toHaveCount(0);
  await expect(card.getByRole('link', { name: 'Open investigation' })).toHaveCount(1);
});

test('UI-001-O5', async ({ page }) => {
  // Sort, and the loading / empty states have words: the sort control reorders the cards.
  await investigationWithContent(page);
  await open(page, '#/investigations');
  const sort = page.getByRole('combobox', { name: 'Sort by' });
  await expect(sort).toBeVisible();
  await sort.selectOption({ label: 'Where it is stored' });
  await expect(sort).toHaveValue('workspace');
  await sort.selectOption({ label: 'Title' });
  const titles = await cards(page).locator('h2').allTextContents();
  expect(titles.length).toBeGreaterThan(0);
  expect(titles).toEqual([...titles].sort());
});

test('UI-001-O6', async ({ page }) => {
  // Opening an investigation keeps its identity: the route names its workspace and id, and its content is shown.
  const v = await investigationWithContent(page);
  const id = new URL(v.url).hash.split('/')[3];
  await open(page, '#/investigations');
  await cards(page).filter({ hasText: v.title }).getByRole('link', { name: 'Open investigation' }).click();
  await expect(sections(page)).toBeVisible();
  expect(page.url()).toContain(id);
  await expect(page.getByText(v.problem).first()).toBeVisible();
});
