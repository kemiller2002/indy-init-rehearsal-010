// Owner: integration. WDL-001 (T24): the workflow layout engine is identified (owner, package, API, version, licence,
// constraints) and shown working in this build.
//
// Discovery record (also the Praxis dependency-decision evidence of WI-0015 [T24]):
//   engine      Forma workflow (Forma.Workflow, F# on .NET WebAssembly) behind the <forma-workflow> element
//   owner       kemiller2002/forma, packages/workflow (Echelon Foundry Forma)
//   package     npm @echelon-foundry/forma-workflow 1.2.0 (the release echelon-current selects with Forma 0.12+;
//               npm provenance); peer @echelon-foundry/design-system >=0.4.0 (this build: 0.13.0)
//   API         host contract forma-workflow-host/1: load, validate, select, focusObject, getWorkflow; events
//               forma-workflow-ready/-load/-selection/-intent/-error; document: Forma portable workflow format 1.0.0
//               (schema forma-workflow.schema.json, capability contract workflow-capabilities.json: workflow.layout
//               "the deterministic default layout", workflow.core "the accessible relationship list")
//   licence     PolyForm-Noncommercial-1.0.0 (constraint: non-commercial use only)
//   constraints a second .NET runtime (~9 MB shipped) loaded on first use; no NuGet package, so the in-process F# API
//               (Layout.resolve) is not available to Causa.App; a document is loaded through load() only (the inline
//               source is read once at connect), hence the boundary glue; text fields are limited to 1000 characters
//   upstream    none required. Observed: at 390 px the Objects disclosure summary is 8 px wider than the viewport
//               (Causa contains it in a scroll region; worth an upstream fix).
import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

const root = new URL('../../../', import.meta.url);
const json = (path) => JSON.parse(readFileSync(new URL(path, root), 'utf8'));

test('WDL-001-O1', async () => {
  // Package, version, owner, licence and API are pinned and as recorded above.
  expect(json('package.json').dependencies['@echelon-foundry/forma-workflow']).toBe('1.2.0');
  const locked = json('package-lock.json').packages['node_modules/@echelon-foundry/forma-workflow'];
  expect(locked.version).toBe('1.2.0');
  expect(locked.integrity).toMatch(/^sha512-/);
  expect(locked.license).toBe('PolyForm-Noncommercial-1.0.0');
  const installed = json('node_modules/@echelon-foundry/forma-workflow/package.json');
  expect(installed.repository.url).toContain('kemiller2002/forma');
  expect(installed.repository.directory).toBe('packages/workflow');
  expect(Object.keys(installed.exports)).toEqual(expect.arrayContaining(['.', './forma-workflow.css', './schema.json', './capabilities.json', './engine/*']));
  const capabilities = json('node_modules/@echelon-foundry/forma-workflow/dist/workflow-capabilities.json');
  expect(Object.keys(capabilities.capabilities)).toEqual(expect.arrayContaining(['workflow.core', 'workflow.layout', 'workflow.embed']));
  const element = readFileSync(new URL('node_modules/@echelon-foundry/forma-workflow/dist/forma-workflow.js', root), 'utf8');
  expect(element).toContain('export const PROTOCOL = "forma-workflow-host/1"');
  for (const method of ['load(workflow)', 'validate(workflow)', 'select(objects)']) expect(element).toContain(method);
});

test('WDL-001-O2', async ({ page }) => {
  // A working example in this build: the shipped element (dist/forma-workflow) lays out Forma's own reference
  // workflow, with every node placed by the engine and the text relationship list present.
  const dist = new URL('dist/', root);
  await page.route('**/__wdl001__/**', (route) => {
    const path = new URL(route.request().url()).pathname.replace('/__wdl001__/', '');
    if (path === 'index.html') return route.fulfill({ contentType: 'text/html', body: `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Workflow example</title>
<link rel="stylesheet" href="forma/all.css"><link rel="stylesheet" href="forma-workflow/forma-workflow.css">
<script type="module" src="forma-workflow/forma-workflow.js"></script></head><body><forma-workflow id="example" mode="inspect"></forma-workflow></body></html>` });
    return route.fulfill({ path: new URL(path, dist).pathname });
  });
  await page.goto('/__wdl001__/index.html');
  const example = readFileSync(new URL('node_modules/@echelon-foundry/design-system/examples/workflows/forma/workflows/branching.forma-workflow.json', root), 'utf8');
  const report = await page.evaluate(async (text) => {
    await customElements.whenDefined('forma-workflow');
    const el = document.getElementById('example');
    const v = await el.validate(text);
    await el.load(text);
    return { valid: v.valid, class: v.class, nodes: JSON.parse(text).nodes.length };
  }, example);
  expect(report.valid).toBe(true);
  expect(report.class).toBe('fully-supported');
  const nodes = page.locator('forma-workflow .ef-diagram-node');
  await expect(nodes).toHaveCount(report.nodes);
  for (const style of await nodes.evaluateAll((ns) => ns.map((n) => n.getAttribute('style')))) expect(style).toMatch(/--ef-diagram-x: [\d.]+rem/);
  await expect(page.locator('forma-workflow').getByRole('list', { name: 'Connections' }).getByRole('button').first()).toBeVisible();
});
