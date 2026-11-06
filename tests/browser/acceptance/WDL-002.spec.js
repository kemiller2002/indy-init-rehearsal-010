// Owner: integration. WDL-002 (T24): no second graph layout implementation and no fixed coordinates; no fallback is
// presented as meeting the requirement.
import { test, expect } from '@playwright/test';
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';

const root = new URL('../../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');
const files = (dir, ext) => {
  const base = new URL(dir, root);
  if (!existsSync(base)) return [];
  return readdirSync(base).flatMap((name) => {
    const rel = `${dir}${name}`;
    if (['bin', 'obj'].includes(name)) return [];
    return statSync(new URL(rel, root)).isDirectory() ? files(`${rel}/`, ext) : ext.some((e) => name.endsWith(e)) ? [rel] : [];
  });
};

test('WDL-002-O1', async () => {
  // No other layout library is a dependency, and no Causa source or screen draws diagram geometry itself.
  const pkg = JSON.parse(read('package.json'));
  const deps = Object.keys({ ...pkg.dependencies, ...pkg.devDependencies });
  for (const banned of ['dagre', 'elkjs', 'cytoscape', 'd3', 'd3-force', 'd3-dag', 'graphlib', 'vis-network', 'mermaid', 'jointjs']) expect(deps).not.toContain(banned);
  expect(deps).toContain('@echelon-foundry/forma-workflow');
  const sources = [...files('src/', ['.fs']), ...files('web/', ['.html', '.css', '.js'])];
  expect(sources.length).toBeGreaterThan(5);
  for (const f of sources) {
    const text = read(f);
    expect(text, f).not.toMatch(/<svg[^>]*class="[^"]*(graph|diagram)|--ef-diagram-x|(?:longestPath|crossingMinimi[sz]ation|sugiyama|forceSimulation)/i);
  }
});

test('WDL-002-O2', async () => {
  // The engine is wired as the one layout path (build ships it, the shell loads it, the boundary hands it documents);
  // no fallback layout exists, so none can be presented as satisfying the requirement.
  expect(read('build.fsx')).toContain('node_modules/@echelon-foundry/forma-workflow/dist');
  const shell = read('web/index.html');
  expect(shell).toContain('forma-workflow/forma-workflow.js');
  expect(shell).toContain('forma-workflow/forma-workflow.css');
  const boundary = read('web/boundary.js');
  expect(boundary).toContain('element.load(text)');
  for (const f of [...files('src/', ['.fs']), ...files('web/', ['.html', '.js'])]) expect(read(f), f).not.toMatch(/fallback (graph|layout|diagram)/i);
});
