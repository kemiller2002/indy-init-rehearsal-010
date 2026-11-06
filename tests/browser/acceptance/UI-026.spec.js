// Owner: ui. UI-026: the Causal Analysis section. Its accessible text relationship list is the P0 floor shipped here;
// the drawn graph on the WDL workflow layout is not in this build (UI-026-O2 is expected to fail until it is).
import { test, expect } from '@playwright/test';
import { investigationWithContent, section } from './workspace-data.mjs';

const list = (page) => page.getByRole('list', { name: 'Evidence and hypothesis relationships' });

test('UI-026-O1', async ({ page }) => {
  // A dedicated Causal Analysis section with the relationships between evidence and hypotheses.
  const v = await investigationWithContent(page);
  await section(page, 'Causal Analysis');
  await expect(page.getByRole('heading', { name: 'Causal Analysis', level: 2 })).toBeVisible();
  await expect(list(page).getByRole('listitem')).toHaveCount(2);
  await expect(list(page)).toContainText(`${v.e1} supports ${v.h1}`);
  await expect(list(page)).toContainText(`${v.e2} contradicts ${v.h1}`);
});

test('UI-026-O2', async ({ page }) => {
  // The graph is laid out by the Echelon workflow layout system (WDL), not hard-coded coordinates.
  await investigationWithContent(page);
  await section(page, 'Causal Analysis');
  await expect(page.locator('[data-wdl-layout], .ef-workflow, .ef-diagram').first()).toBeVisible();
});

test('UI-026-O3', async ({ page }) => {
  // Selecting a relationship's hypothesis reveals that hypothesis with its evidence, and links to the records.
  const v = await investigationWithContent(page);
  await section(page, 'Causal Analysis');
  await list(page).getByRole('link', { name: v.h1 }).first().click();
  const inspector = page.getByRole('complementary').filter({ hasText: v.h1 });
  await expect(inspector).toBeVisible();
  await expect(inspector.getByRole('link')).toContainText([`supports: ${v.e1}`, `contradicts: ${v.e2}`, 'Close details']);
});

test('UI-026-O4', async ({ page }) => {
  // A text equivalent, and read-only: nothing in the section edits the domain.
  await investigationWithContent(page);
  await section(page, 'Causal Analysis');
  const sectionEl = page.locator('[data-requirement-id="UI-026"]');
  await expect(sectionEl.getByRole('listitem').first()).toBeVisible();
  await expect(sectionEl.getByRole('textbox')).toHaveCount(0);
  await expect(sectionEl.getByRole('button')).toHaveCount(0);
  await expect(sectionEl.locator('[draggable="true"]')).toHaveCount(0);
});
