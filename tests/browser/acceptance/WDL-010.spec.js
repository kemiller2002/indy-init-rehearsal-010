// Owner: ui. WDL-010: selecting a node resolves to the investigation's record; nothing edits the domain.
import { test, expect } from '@playwright/test';
import { renderGraph, openSeededGraph } from './workspace-data.mjs';

test('WDL-010-O1', async ({ page }) => {
  // In the fixture the selected node names its record and its link; in the app the link opens that record.
  const { el, workflow } = await renderGraph(page, 'four-roles');
  const node = workflow.nodes.find((n) => n.label === 'The pump seal failed');
  await el.getByRole('list', { name: 'Steps' }).getByRole('button', { name: /The pump seal failed/ }).click();
  await expect(el.getByRole('link', { name: 'Open the hypothesis' }).first()).toHaveAttribute('href', node.references[0].href);
  expect(node.references[0].href).toMatch(new RegExp(`/hypotheses\\?record=${node.references[0].key}$`));
  const live = await openSeededGraph(page);
  await live.getByRole('list', { name: 'Steps' }).getByRole('button').filter({ hasText: 'Hypothesis' }).first().click();
  await live.getByRole('link', { name: 'Open the hypothesis' }).first().click();
  expect(page.url()).toMatch(/\/hypotheses\?record=[0-9a-f-]{36}$/);
  await expect(page.getByRole('complementary').locator('.ef-eyebrow')).toHaveText('Hypothesis');
});

test('WDL-010-O2', async ({ page }) => {
  // Read-only: the graph is mounted in inspect mode, with no create, connect, delete, move or undo commands.
  const { el } = await renderGraph(page, 'four-roles');
  await expect(el).toHaveAttribute('mode', 'inspect');
  await expect(el.getByRole('button')).not.toHaveCount(0);
  await expect(el.getByRole('button', { name: /^(Add|Connect|Delete|Undo|Redo|Move|Auto layout|Arrange)/ })).toHaveCount(0);
  await expect(el.locator('[draggable="true"]')).toHaveCount(0);
  await expect(el.locator('.ef-workflow-editor')).toHaveAttribute('data-mode', 'inspect');
});
