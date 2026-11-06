// Owner: ui. UI-011: accessibility and responsive behaviour of the shell and its destinations (T16), judged with the
// pinned Forma layout kit (FL-01..FL-05) where a rule exists.
import { test, expect } from '@playwright/test';
import { load, open, started, primary, viewports, routes, catalogInvestigation } from '../support/app.mjs';
import { skipLinkFirst, focusVisible, touchTargets, noHorizontalScroll, textNotClipped, reducedMotion } from '../support/layout-assertions.mjs';

const pages = [routes.Investigations, routes.Administration, routes.Settings, catalogInvestigation, '#/no-such-page'];

test('UI-011-O1', async ({ page }) => {
  for (const route of pages) {
    await open(page, route);
    // Landmarks: banner, a labelled primary navigation, main; one page heading.
    await expect(page.getByRole('banner'), route).toHaveCount(1);
    await expect(page.getByRole('navigation', { name: 'Primary' }), route).toHaveCount(1);
    await expect(page.getByRole('main'), route).toHaveCount(1);
    // One page heading on each destination; an investigation's heading is its frame's (the workspace slice), so
    // there the shell adds none of its own.
    if (route === catalogInvestigation) expect(await page.getByRole('heading', { level: 1 }).count(), route).toBeLessThanOrEqual(1);
    else await expect(page.getByRole('heading', { level: 1 }), route).toHaveCount(1);
    // Touch targets of at least 44 px.
    expect(await touchTargets(page), `${route}: touch targets`).toEqual([]);
    // Every control has an accessible name.
    const unnamed = await page.locator('main input, main button, main select, main textarea').evaluateAll((els) =>
      els.filter((e) => !(e.labels && e.labels.length) && !e.getAttribute('aria-label') && !(e.textContent || '').trim()).length);
    expect(unnamed, `${route}: unlabelled controls`).toBe(0);
    // The current page is marked by more than colour: aria-current plus weight or underline.
    const nav = await primary(page);
    const current = nav.locator('[aria-current="page"]');
    if (await current.count()) {
      const cue = await current.evaluate((a) => { const s = getComputedStyle(a); return parseInt(s.fontWeight, 10) >= 700 || s.textDecorationLine.includes('underline'); });
      expect(cue, `${route}: current page has a non-colour cue`).toBe(true);
    }
  }
  // Keyboard: the skip link comes first and works; every focus stop shows a visible ring.
  await open(page, routes.Settings);
  expect(await skipLinkFirst(page)).toEqual([]);
  await page.keyboard.press('Enter');
  expect(await page.evaluate(() => document.activeElement && document.activeElement.id)).toBe('main');
  await open(page, routes.Settings);
  expect(await focusVisible(page)).toEqual([]);
  // A semantic status announces loading before the engine starts.
  await page.route(/_framework\/dotnet\.js$/, async (route) => { await new Promise((r) => setTimeout(r, 1500)); await route.continue(); });
  await load(page, routes.Investigations, { waitUntil: 'commit' });
  await expect(page.getByRole('status').filter({ hasText: 'Loading Causa…' })).toBeVisible();
  await started(page);
});

test('UI-011-O2', async ({ page }) => {
  // iPad portrait/landscape, narrow mobile and desktop: no horizontal scroll and no clipped text on any destination.
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    for (const route of pages) {
      await open(page, route);
      expect(await noHorizontalScroll(page), `${vp.id} ${route}`).toEqual([]);
      expect(await textNotClipped(page), `${vp.id} ${route}`).toEqual([]);
    }
  }
  // 200% text reflows: at 640 px with the root text doubled (WCAG 1.4.4 / 1.4.10), nothing scrolls sideways.
  await page.setViewportSize({ width: 640, height: 900 });
  for (const route of pages) {
    await open(page, route);
    await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
    expect(await noHorizontalScroll(page), `200% ${route}`).toEqual([]);
  }
  // Reduced motion: under the system preference, and under Causa's own Motion preference, nothing animates.
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.route(/_framework\/dotnet\.js$/, async (route) => { await new Promise((r) => setTimeout(r, 2500)); await route.continue(); });
  await load(page, routes.Investigations, { waitUntil: 'commit' });
  expect(await reducedMotion(page), 'system reduced motion while loading').toEqual([]);
  await started(page);
  await page.unroute(/_framework\/dotnet\.js$/);
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await open(page, routes.Settings);
  await page.getByRole('radio', { name: 'Reduce motion' }).check();
  await expect(page.locator('.causa-app')).toHaveAttribute('data-motion', 'reduce');
  expect(await reducedMotion(page), 'Causa motion preference').toEqual([]);
  // Screen readers: the shell's accessibility tree names the navigation, the page and its heading.
  const tree = await page.locator('body').ariaSnapshot();
  expect(tree).toContain('navigation "Primary"');
  expect(tree).toContain('main');
  expect(tree).toMatch(/heading "Settings" \[level=1\]/);
});

test('UI-011-O3', async ({ page }) => {
  for (const route of pages) {
    await open(page, route);
    // Nothing needs dragging.
    await expect(page.locator('[draggable="true"]'), route).toHaveCount(0);
    // Nothing is revealed only on hover: no stylesheet rule under :hover changes display, visibility or opacity.
    const hoverReveals = await page.evaluate(() => [...document.styleSheets].flatMap((sheet) => { try { return [...sheet.cssRules]; } catch { return []; } })
      .flatMap((r) => (r.cssRules ? [...r.cssRules] : [r]))
      .filter((r) => r.selectorText && r.selectorText.includes(':hover') && (r.style.display || r.style.visibility || r.style.opacity))
      .filter((r) => !r.selectorText.startsWith('.ef-') || r.selectorText.includes('causa'))
      .map((r) => r.selectorText));
    expect(hoverReveals, route).toEqual([]);
    // Every link and button is reachable by keyboard (none is pointer-only).
    const unreachable = await page.locator('a[href], button').evaluateAll((els) => els.filter((e) => e.offsetParent !== null && e.tabIndex < 0).length);
    expect(unreachable, route).toBe(0);
  }
});

test('UI-011-O4', async ({ page }) => {
  // WebAssembly handoff: focus and scroll position set while the engine loads survive its start.
  await page.setViewportSize({ width: 1440, height: 400 });
  await page.route(/_framework\/dotnet\.js$/, async (route) => { await new Promise((r) => setTimeout(r, 2000)); await route.continue(); });
  await load(page, routes.Investigations, { waitUntil: 'commit' });
  const nav = page.locator('nav.ef-navigation-shell__persistent');
  await nav.getByRole('link', { name: 'Administration', exact: true }).focus();
  await page.evaluate(() => window.scrollTo(0, 40));
  const scrolled = await page.evaluate(() => window.scrollY);
  await started(page);
  expect(await page.evaluate(() => document.activeElement && document.activeElement.textContent)).toBe('Administration');
  expect(await page.evaluate(() => window.scrollY)).toBe(scrolled);
  // Transitions: following a navigation link keeps focus on it, not on the document body.
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { level: 1, name: 'Administration' })).toBeVisible();
  expect(await page.evaluate(() => document.activeElement !== document.body)).toBe(true);
});

test('UI-011-O5', async ({ page }) => {
  // Graphs need an equivalent text interaction: every graph drawing on any shell page or investigation section
  // (SVG or canvas, other than the Causa mark) has an accessible name and a linked text alternative.
  for (const route of [...pages, catalogInvestigation.replace('overview', 'causal-analysis')]) {
    await open(page, route);
    const graphs = await page.locator('main svg:not(.causa-mark), main canvas').evaluateAll((els) => els.map((e) => ({
      named: !!(e.getAttribute('aria-label') || e.getAttribute('aria-labelledby')),
      described: (e.getAttribute('aria-describedby') || '').split(/\s+/).some((id) => id && document.getElementById(id) && document.getElementById(id).textContent.trim()),
    })));
    for (const g of graphs) expect(g, route).toEqual({ named: true, described: true });
    // The Causa mark itself has a text equivalent.
    await expect(page.getByRole('img', { name: 'Causa' })).toHaveCount(1);
  }
});
