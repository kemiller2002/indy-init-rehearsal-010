// Owner: ui. WDL-009: same input, same layout; stable ids and order across refreshes.
import { test, expect } from '@playwright/test';
import { renderGraph, nodeBoxes } from './workspace-data.mjs';

test('WDL-009-O1', async ({ page }) => {
  const first = await nodeBoxes((await renderGraph(page, 'shared-members')).el);
  await page.reload();
  const second = await nodeBoxes((await renderGraph(page, 'shared-members')).el);
  expect(first.length).toBe(4);
  expect(second).toEqual(first);
});

test('WDL-009-O2', async ({ page }) => {
  // Node ids are the records' ids and keep their order on refresh.
  const { el, workflow } = await renderGraph(page, 'four-roles');
  const ids = await el.locator('.ef-diagram-node').evaluateAll((ns) => ns.map((n) => n.id));
  expect(ids.length).toBe(5);
  for (const n of workflow.nodes) expect(n.id).toMatch(/^[hc]-[0-9a-f-]{36}$/);
  await page.reload();
  const again = await (await renderGraph(page, 'four-roles')).el.locator('.ef-diagram-node').evaluateAll((ns) => ns.map((n) => n.id));
  expect(again).toEqual(ids);
});
