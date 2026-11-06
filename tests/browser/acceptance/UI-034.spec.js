// Owner: ui. UI-034: one Forma card inspector for every kind of selection, at every viewport, by keyboard.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { viewports } from '../support/app.mjs';
import { investigationWithContent, section } from './workspace-data.mjs';

test('UI-034-O1', async ({ page }) => {
  // The same inspector shows evidence, hypotheses, assessments and timeline selections.
  const v = await investigationWithContent(page);
  const kinds = [['Evidence', v.e1, 'Evidence'], ['Hypotheses', v.h1, 'Hypothesis']];
  for (const [name, text, kind] of kinds) {
    await section(page, name);
    await page.getByRole('article').filter({ hasText: text }).getByRole('link', { name: 'Details' }).click();
    const inspector = page.getByRole('complementary');
    await expect(inspector).toHaveClass(/ef-card causa-inspector/);
    await expect(inspector.locator('.ef-eyebrow')).toHaveText(kind);
  }
  await section(page, 'Causal Analysis');
  await page.getByRole('listitem').filter({ hasText: v.r1 }).getByRole('link', { name: 'Details' }).click();
  await expect(page.getByRole('complementary').locator('.ef-eyebrow')).toHaveText('Assessment');
});

test('UI-034-O2', async ({ page }) => {
  // At every viewport the inspector is reachable, closes by keyboard, and the selection is marked.
  const v = await investigationWithContent(page);
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await section(page, 'Hypotheses');
    const details = page.getByRole('article').filter({ hasText: v.h1 }).getByRole('link', { name: 'Details' });
    await details.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('complementary'), vp.id).toBeVisible();
    await expect(page.getByRole('article').filter({ hasText: v.h1 }).getByRole('link', { name: 'Details' }), vp.id).toHaveAttribute('aria-current', 'true');
    const close = page.getByRole('link', { name: 'Close details' });
    await close.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('complementary'), vp.id).toHaveCount(0);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, vp.id).toBeLessThanOrEqual(0);
  }
});

test('UI-034-O3', async () => {
  // Component ownership: the inspector is the ui-3 Inspector module over Forma's card, used by every section.
  const inspector = readFileSync(new URL('../../../src/Causa.App/Views/Workspace/Inspector.fs', import.meta.url), 'utf8');
  expect(inspector).toContain('// Owner: ui-3.');
  for (const f of ['workspace-overview', 'workspace-hypotheses', 'workspace-evidence', 'workspace-causal-analysis', 'workspace-timeline']) {
    const html = readFileSync(new URL(`../../../web/screens/${f}.html`, import.meta.url), 'utf8');
    expect(html, f).toContain('class="ef-card causa-inspector"');
  }
});
