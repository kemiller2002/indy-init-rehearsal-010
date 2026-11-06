// Owner: ui. WDL-011: the deterministic graph fixtures, produced by the same projection the real provider's
// histories go through.
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { renderGraph, graphFixtures, openSeededGraph } from './workspace-data.mjs';

test('WDL-011-O1', async ({ page }) => {
  // Every named fixture exists and renders: empty, single node, four roles, dense evidence, long labels,
  // disconnected, and the permitted (undirected) cycle.
  expect(graphFixtures).toEqual(['empty', 'single-node', 'four-roles', 'dense-evidence', 'long-labels', 'disconnected', 'shared-members']);
  for (const name of graphFixtures) {
    const { report, workflow } = await renderGraph(page, name);
    expect(report.valid, name).toBe(true);
    expect(workflow.id, name).toBe('causal-graph');
  }
  const shared = JSON.parse(readFileSync(new URL('./wdl-fixtures/shared-members.forma-workflow.json', import.meta.url), 'utf8'));
  const outcomes = new Set(shared.edges.map((e) => e.target.node));
  expect(outcomes.size).toBe(2);
  expect(shared.edges.length).toBe(4);
});

test('WDL-011-O2', async ({ page }) => {
  // The fixtures come from Views/Graph.fs (proved byte-for-byte by CausalGraphTests); the app's graph of a real
  // stored history is that same projection: the same document shape, ids and kinds.
  const tests = readFileSync(new URL('../../Causa.Contract.Tests/Report/Producer.fs', import.meta.url), 'utf8');
  expect(tests).toContain('the committed browser fixtures are exactly the projection');
  const live = await openSeededGraph(page);
  const doc = JSON.parse(await live.getAttribute('data-workflow'));
  expect(doc.id).toBe('causal-graph');
  expect(doc.nodes.length).toBeGreaterThan(0);
  for (const n of doc.nodes) expect(['org.causa:hypothesis', 'org.causa:outcome']).toContain(n.kind);
  for (const n of doc.nodes) expect(n.id).toMatch(/^[hc]-[0-9a-f-]{36}$/);
});
