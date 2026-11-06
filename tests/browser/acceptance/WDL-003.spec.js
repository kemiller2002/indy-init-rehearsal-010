// Owner: ui. WDL-003: the typed adapter projects causal assertions, outcomes, hypotheses, cause roles and evidence
// counts into the graph (Views/Graph.fs CausalGraph), and the rendered graph shows exactly those.
import { test, expect } from '@playwright/test';
import { renderGraph } from './workspace-data.mjs';

test('WDL-003-O1', async ({ page }) => {
  const { el, workflow } = await renderGraph(page, 'four-roles');
  const nodes = el.locator('.ef-diagram-node');
  await expect(nodes).toHaveCount(5);
  await expect(nodes.filter({ hasText: 'Outcome' })).toHaveText([/Coolant leak in loop B/]);
  for (const h of ['The pump seal failed', 'A pressure spike occurred at start-up', 'Maintenance was deferred twice', 'The relief valve was set too high']) await expect(nodes.filter({ hasText: h })).toContainText('Hypothesis');
  const connections = el.getByRole('list', { name: 'Connections' }).getByRole('button');
  await expect(connections).toHaveCount(4);
  for (const role of ['root cause', 'trigger', 'contributing factor', 'enabling condition']) await expect(connections.filter({ hasText: role })).toHaveCount(1);
  expect(workflow.edges.map((e) => e.kindLabel)).toEqual(['root cause', 'trigger', 'contributing factor', 'enabling condition']);
  const dense = await renderGraph(page, 'dense-evidence');
  await expect(dense.el.locator('.ef-diagram-node').filter({ hasText: 'The pump seal failed' })).toContainText('Evidence: 8 supporting, 4 contradicting');
});

test('WDL-003-O2', async ({ page }) => {
  // No invented claim: a hypothesis in no causal assertion has no connection; every node's status is a domain word.
  const { el, workflow } = await renderGraph(page, 'disconnected');
  const isolated = workflow.nodes.find((n) => n.label === 'An operator misread the gauge');
  expect(isolated).toBeTruthy();
  expect(workflow.edges.filter((e) => e.source.node === isolated.id || e.target.node === isolated.id)).toEqual([]);
  await expect(el.getByRole('list', { name: 'Connections' }).getByRole('button')).toHaveCount(2);
  for (const n of workflow.nodes) expect(['proposed', 'plausible', 'supported', 'weakened', 'falsified', 'superseded', 'confirmed', 'reopened']).toContain(n.status.label);
});
