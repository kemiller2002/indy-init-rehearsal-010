// Owner: ui. WDL-005: placement is computed from the graph, and stays valid for every graph shape the domain allows.
import { test, expect } from '@playwright/test';
import { renderGraph, nodeBoxes, overlaps, graphFixtures } from './workspace-data.mjs';

test('WDL-005-O1', async ({ page }) => {
  // Different graphs get different, computed placements; nothing is fixed per investigation.
  const a = await nodeBoxes((await renderGraph(page, 'four-roles')).el);
  const b = await nodeBoxes((await renderGraph(page, 'disconnected')).el);
  expect(a.length).toBe(5);
  expect(b.length).toBe(5);
  expect(JSON.stringify(a.map((n) => [n.x, n.y]))).not.toEqual(JSON.stringify(b.map((n) => [n.x, n.y])));
});

test('WDL-005-O2', async ({ page }) => {
  // Zero, one, many, disconnected and the permitted cycle all render as valid, fully supported, overlap-free layouts.
  const expected = { empty: 0, 'single-node': 1, 'four-roles': 5, 'dense-evidence': 2, 'long-labels': 2, disconnected: 5, 'shared-members': 4 };
  for (const name of graphFixtures) {
    const { el, report } = await renderGraph(page, name);
    expect(report, name).toEqual({ valid: true, class: 'fully-supported' });
    const boxes = await nodeBoxes(el);
    expect(boxes.length, name).toBe(expected[name]);
    expect(overlaps(boxes), name).toEqual([]);
  }
});
