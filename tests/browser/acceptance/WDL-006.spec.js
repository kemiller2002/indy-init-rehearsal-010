// Owner: ui. WDL-006: no overlaps, clipped labels, unreachable connectors or hidden evidence counts; crossings are
// kept low by the engine's documented layered layout (no promise of zero for arbitrary graphs).
import { test, expect } from '@playwright/test';
import { renderGraph, nodeBoxes, overlaps, graphFixtures } from './workspace-data.mjs';

const clipped = (el) => el.locator('.ef-diagram-node').evaluateAll((ns) => ns.flatMap((n) =>
  [...n.querySelectorAll('h3, p')].filter((t) => t.scrollWidth > t.clientWidth + 1 || t.scrollHeight > t.clientHeight + 1).map((t) => `${n.id}: ${t.textContent.slice(0, 40)}`)));

test('WDL-006-O1', async ({ page }) => {
  for (const name of graphFixtures.filter((n) => n !== 'empty')) {
    const { el, workflow } = await renderGraph(page, name);
    expect(overlaps(await nodeBoxes(el)), name).toEqual([]);
    expect(await clipped(el), name).toEqual([]);
    // Every connector is reachable as a named object, and every hypothesis shows its evidence count.
    await expect(el.getByRole('list', { name: 'Connections' }).getByRole('button'), name).toHaveCount((workflow.edges || []).length);
    for (const n of workflow.nodes.filter((x) => x.kind === 'org.causa:hypothesis')) await expect(el.locator('.ef-diagram-node').filter({ hasText: n.label.slice(0, 60) }).getByText(/^Evidence: \d+ supporting, \d+ contradicting/), name).toBeVisible();
  }
});

test('WDL-006-O2', async ({ page }) => {
  // The layered layout keeps edge crossings low: the four-role fan-in and the disconnected graph cross nowhere.
  const crossings = async (el) => el.locator('path.ef-diagram-connector').evaluateAll((ps) => {
    const segs = ps.map((p) => { const pts = p.getAttribute('d').match(/[\d.]+ [\d.]+/g).map((s) => s.split(' ').map(Number)); return pts.slice(1).map((q, i) => [pts[i], q]); });
    const cross = ([a, b], [c, d]) => { const o = (p, q, r) => Math.sign((q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])); return o(a, b, c) * o(a, b, d) < 0 && o(c, d, a) * o(c, d, b) < 0; };
    let n = 0;
    for (let i = 0; i < segs.length; i++) for (let j = i + 1; j < segs.length; j++) for (const s of segs[i]) for (const t of segs[j]) if (cross(s, t)) n++;
    return n;
  });
  for (const name of ['four-roles', 'disconnected']) {
    const { el } = await renderGraph(page, name);
    expect(await el.locator('path.ef-diagram-connector').count(), name).toBeGreaterThan(0);
    expect(await crossings(el), name).toBe(0);
  }
});
