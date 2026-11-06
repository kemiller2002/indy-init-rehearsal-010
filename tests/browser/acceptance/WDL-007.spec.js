// Owner: ui. WDL-007: the graph works on desktop, iPad and mobile, by keyboard, with a text equivalent, and never by
// colour alone.
import { test, expect } from '@playwright/test';
import { viewports } from '../support/app.mjs';
import { noHorizontalScroll, scrollRegionsAccessible, reducedMotion, textContrast } from '../support/layout-assertions.mjs';
import { renderGraph } from './workspace-data.mjs';

test('WDL-007-O1', async ({ page }) => {
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    const { el } = await renderGraph(page, 'four-roles');
    // Text equivalent: every node and connection is a named button in the objects list.
    await expect(el.getByRole('list', { name: 'Steps' }).getByRole('button'), vp.id).toHaveCount(5);
    // The canvas scrolls inside its own named, focusable region; the page itself does not scroll sideways.
    expect(await noHorizontalScroll(page), vp.id).toEqual([]);
    expect(await scrollRegionsAccessible(page), vp.id).toEqual([]);
    await expect(el.getByRole('group', { name: /diagram/ }), vp.id).toHaveAttribute('tabindex', '0');
    // Keyboard: an object is selected from the list with Enter and stays focused.
    const first = el.getByRole('list', { name: 'Steps' }).getByRole('button').first();
    await first.focus();
    await page.keyboard.press('Enter');
    await expect(el.getByRole('list', { name: 'Steps' }).getByRole('button').first(), vp.id).toHaveAttribute('aria-pressed', 'true');
  }
  // Text in the graph meets contrast.
  expect(await textContrast(page)).toEqual([]);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await renderGraph(page, 'four-roles');
  expect(await reducedMotion(page)).toEqual([]);
});

test('WDL-007-O2', async ({ page }) => {
  // States are words on each node, roles are words on each connection and line styles, and the legend explains them.
  const { el, workflow } = await renderGraph(page, 'shared-members');
  for (const node of await el.locator('.ef-diagram-node').all()) await expect(node).toContainText(/Status\s*(proposed|confirmed|reopened|superseded|plausible|supported|weakened|falsified)/);
  const lines = workflow.edges.map((e) => e.line);
  expect(new Set(lines).size).toBeGreaterThan(1);
  for (const [role, line] of [['root cause', 'solid'], ['trigger', 'solid'], ['contributing factor', 'dashed'], ['enabling condition', 'dotted']]) {
    await expect(el).toContainText(`A ${line} line from a hypothesis to an outcome: the hypothesis is its ${role}.`);
  }
  // Each connection names its role in the text equivalent, and a dashed one says so.
  await expect(el.getByRole('list', { name: 'Connections' }).getByRole('button').filter({ hasText: 'contributing factor' })).toContainText('dashed line');
});
