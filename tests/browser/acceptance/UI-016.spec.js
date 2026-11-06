// Owner: ui. UI-016: Causa's visual identity: the five palette colours as named theme tokens, in their semantic
// roles, with measured contrast and never as the only cue (T16). Contrast and labelling use the pinned Forma kit (FL-06).
import { test, expect } from '@playwright/test';
import { load, open, started, routes, viewports, catalogInvestigation, paintedColours, rgb, palette } from '../support/app.mjs';
import { textContrast, nonTextContrast, amberLabelled, focusVisible, reducedMotion } from '../support/layout-assertions.mjs';

const pages = [routes.Investigations, routes.Administration, routes.Settings, catalogInvestigation, '#/no-such-page'];
const style = (locator, property) => locator.evaluate((e, p) => getComputedStyle(e)[p].replace(/\s/g, ''), property);
const token = (page, name) => page.evaluate((n) => getComputedStyle(document.documentElement).getPropertyValue(n).trim().toUpperCase(), name);
const luminance = (hex) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)).reduce((a, v, k) => a + v * [0.2126, 0.7152, 0.0722][k], 0);
const contrast = (a, b) => { const [x, y] = [luminance(a), luminance(b)].sort((m, n) => n - m); return (x + 0.05) / (y + 0.05); };
const saturated = (c) => { const m = c.match(/rgba?\((\d+),(\d+),(\d+)/); if (!m) return false; const v = m.slice(1, 4).map(Number); return Math.max(...v) - Math.min(...v) > 30; };
const loadFailed = async (page) => { await page.route(/_framework\/dotnet\.js$/, (r) => r.abort()); await load(page, routes.Investigations); await expect(page.getByRole('alert')).toBeVisible(); };

test('UI-016-O1', async ({ page }) => {
  // A deliberately coloured identity on every page: navy, teal and ivory are painted, not a grey presentation.
  for (const route of pages) {
    await open(page, route);
    const colours = await paintedColours(page);
    for (const c of [palette.navy, palette.teal, palette.ivory]) expect(colours, `${route} paints ${c}`).toContain(rgb(c));
    expect(colours.filter(saturated).length, route).toBeGreaterThan(1);
  }
});

test('UI-016-O2', async ({ page }) => {
  // The canonical palette values are the theme's tokens.
  await open(page, routes.Investigations);
  for (const [name, hex] of Object.entries(palette)) expect(await token(page, `--causa-${name}`), name).toBe(hex.toUpperCase());
});

test('UI-016-O3', async ({ page }) => {
  // Deep navy: primary navigation, masthead, structural emphasis.
  await open(page, routes.Settings);
  expect(await style(page.locator('.causa-masthead'), 'backgroundColor')).toBe(rgb(palette.navy));
  expect(await style(page.locator('nav.ef-navigation-shell__persistent'), 'backgroundColor')).toBe(rgb(palette.navy));
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await style(page.locator('details.ef-navigation-shell__compact'), 'backgroundColor')).toBe(rgb(palette.navy));
});

test('UI-016-O4', async ({ page }) => {
  // Teal: active selection (the current destination and section) and primary actions.
  await open(page, catalogInvestigation);
  expect(await style(page.locator('nav.ef-navigation-shell__persistent [aria-current="page"]'), 'borderLeftColor')).toBe(rgb(palette.teal));
  expect(await style(page.getByRole('navigation', { name: 'Investigation sections' }).locator('[aria-current="page"]'), 'borderBottomColor')).toBe(rgb(palette.teal));
  await open(page, '#/no-such-page');
  expect(await style(page.locator('.causa-not-found .causa-action'), 'borderTopColor')).toBe(rgb(palette.teal));
  await open(page, routes.Settings);
  expect(await style(page.locator('.ef-choice:has(input:checked)').first(), 'borderLeftColor')).toBe(rgb(palette.teal));
});

test('UI-016-O5', async ({ page }) => {
  // Amber: attention: the one thing Administration needs from the user before connecting, labelled in words.
  await open(page, routes.Administration);
  const attention = page.locator('main .ef-status-lozenge[data-state="attention"]');
  await expect(attention).toHaveText(/Needs attention/);
  expect(await style(attention, 'backgroundColor')).toBe(rgb(palette.amber));
});

test('UI-016-O6', async ({ page }) => {
  // Coral: critical alerts: the application failing to start.
  await loadFailed(page);
  expect(await style(page.locator('#causa-load-failed'), 'borderLeftColor')).toBe(rgb(palette.coral));
});

test('UI-016-O7', async ({ page }) => {
  // Soft ivory: the main workspace surface.
  for (const route of pages) {
    await open(page, route);
    expect(await style(page.locator('body'), 'backgroundColor'), route).toBe(rgb(palette.ivory));
  }
});

test('UI-016-O8', async ({ page, request }) => {
  // Colours are named theme tokens: no literal colour in any Causa stylesheet except as a token definition in the
  // theme file, and no colour in a style attribute.
  await open(page, routes.Administration);
  const sheets = await page.locator('link[rel=stylesheet]').evaluateAll((ls) => ls.map((l) => l.href).filter((h) => !h.includes('/forma/')));
  expect(sheets.length).toBeGreaterThanOrEqual(2);
  const literal = /#[0-9a-f]{3,8}\b|\brgba?\(|\bhsla?\(|\boklch\(/i;
  for (const href of sheets) {
    const css = (await (await request.get(href)).text()).replace(/\/\*[\s\S]*?\*\//g, '');
    const offending = css.split(/[;{}]/).map((d) => d.trim()).filter((d) => literal.test(d))
      .filter((d) => !(href.includes('theme') && /^--causa-[a-z-]+\s*:\s*#[0-9a-f]{6}$/i.test(d)));
    expect(offending, href).toEqual([]);
  }
  await expect(page.locator('[style*="color"]')).toHaveCount(0);
});

test('UI-016-O9', async ({ page }) => {
  // Forma components for surfaces, controls, states and focus: the shell and each destination use Forma's patterns.
  await open(page, routes.Administration);
  for (const c of ['ef-site-header', 'ef-navigation-shell', 'ef-skip-link', 'ef-status-lozenge', 'ef-key-value-list']) await expect(page.locator(`.${c}`).first(), c).toBeAttached();
  await open(page, routes.Settings);
  for (const c of ['ef-choice-group', 'ef-choice', 'ef-form']) await expect(page.locator(`.${c}`).first(), c).toBeAttached();
  await expect(page.locator('link[href="forma/all.css"]')).toHaveCount(1);
  await loadFailed(page);
  for (const c of ['ef-fault', 'ef-spinner']) await expect(page.locator(`.${c}`).first(), c).toBeAttached();
});

test('UI-016-O10', async ({ page }) => {
  // Derived tokens exist because the base colours were measured: ivory text on teal is under 4.5:1, so actions use
  // teal-strong; coral text is under 4.5:1 on light surfaces, so it uses coral-strong; navy on amber passes.
  await open(page, routes.Investigations);
  const t = Object.fromEntries(await Promise.all(['teal', 'teal-strong', 'coral', 'coral-strong', 'ivory', 'amber', 'navy', 'surface'].map(async (n) => [n, await token(page, `--causa-${n}`)])));
  expect(contrast(t.ivory, t.teal)).toBeLessThan(4.5);
  expect(contrast(t.surface, t['teal-strong'])).toBeGreaterThanOrEqual(4.5);
  expect(contrast(t.ivory, t.coral)).toBeLessThan(4.5);
  expect(contrast(t.surface, t['coral-strong'])).toBeGreaterThanOrEqual(4.5);
  expect(contrast(t.navy, t.amber)).toBeGreaterThanOrEqual(4.5);
});

test('UI-016-O11', async ({ page }) => {
  // WCAG AA: text 4.5:1 (3:1 large) and non-text indicators 3:1, on every page, at desktop and mobile.
  for (const vp of [viewports[0], viewports[3]]) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    for (const route of pages) {
      await open(page, route);
      expect(await textContrast(page), `${vp.id} ${route}`).toEqual([]);
      expect(await nonTextContrast(page), `${vp.id} ${route}`).toEqual([]);
    }
  }
  await loadFailed(page);
  expect(await textContrast(page), 'load failure').toEqual([]);
});

test('UI-016-O12', async ({ page }) => {
  // Amber is never the only cue: every amber element carries a visible text label.
  for (const route of pages) {
    await open(page, route);
    expect(await amberLabelled(page), route).toEqual([]);
  }
});

test('UI-016-O13', async ({ page }) => {
  // Status is never colour alone: every lozenge says its state in words, and the current destination has a non-colour cue.
  for (const route of [routes.Administration, catalogInvestigation]) {
    await open(page, route);
    const lozenges = await page.locator('.ef-status-lozenge').allInnerTexts();
    expect(lozenges.length, route).toBeGreaterThan(0);
    for (const l of lozenges) expect(l, route).toMatch(/[A-Za-z]{3,}/);
    const cue = await page.locator('nav.ef-navigation-shell__persistent [aria-current="page"]').evaluate((a) => getComputedStyle(a).textDecorationLine);
    expect(cue, route).toContain('underline');
  }
});

test('UI-016-O14', async ({ page }) => {
  // The startup shell and the views use the same tokens for the same roles.
  await page.route(/_framework\/dotnet\.js$/, async (route) => { await new Promise((r) => setTimeout(r, 2000)); await route.continue(); });
  await load(page, routes.Investigations, { waitUntil: 'commit' });
  await expect(page.getByRole('status').filter({ hasText: 'Loading Causa…' })).toBeVisible();
  const loading = { masthead: await style(page.locator('.causa-masthead'), 'backgroundColor'), surface: await style(page.locator('body'), 'backgroundColor'), mark: await style(page.locator('.causa-masthead .causa-mark__ground'), 'fill') };
  await started(page);
  for (const route of pages) {
    await open(page, route);
    expect({ masthead: await style(page.locator('.causa-masthead'), 'backgroundColor'), surface: await style(page.locator('body'), 'backgroundColor'), mark: await style(page.locator('.causa-masthead .causa-mark__ground'), 'fill') }, route).toEqual(loading);
  }
  expect(loading.masthead).toBe(rgb(palette.navy));
});

test('UI-016-O15', async ({ page }) => {
  // Printed, the shell's pages stay legible in greyscale: navigation chrome is dropped, the page is on white, and
  // every text passes 4.5:1 by luminance (which greyscale preserves).
  for (const route of [routes.Administration, routes.Settings]) {
    await open(page, route);
    await page.emulateMedia({ media: 'print' });
    await expect(page.locator('nav.ef-navigation-shell__persistent')).toBeHidden();
    expect(await style(page.locator('body'), 'backgroundColor')).toBe('rgb(255,255,255)');
    await page.evaluate(() => { document.documentElement.style.filter = 'grayscale(1)'; });
    expect(await textContrast(page), route).toEqual([]);
    await page.emulateMedia({ media: 'screen' });
  }
});

test('UI-016-O16', async ({ page }, info) => {
  // Screenshots at desktop, iPad portrait and landscape and mobile, with the automated token, contrast, focus and
  // reduced-motion checks. (The owner's independent visual review of these screenshots is a separate record.)
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    for (const route of [routes.Investigations, routes.Administration, routes.Settings, catalogInvestigation]) {
      await open(page, route);
      expect(await token(page, '--causa-navy'), vp.id).toBe(palette.navy);
      expect(await textContrast(page), `${vp.id} ${route}`).toEqual([]);
      await info.attach(`${vp.id}-${route.split('/')[1]}`, { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
    }
    await open(page, routes.Settings);
    expect(await focusVisible(page), vp.id).toEqual([]);
  }
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await open(page, routes.Administration);
  expect(await reducedMotion(page)).toEqual([]);
});

test('UI-016-O17', async ({ page }) => {
  // Each palette colour appears where its role applies, and absence of data never takes a status colour: unknown and
  // unavailable facts are neutral, amber only marks attention, coral only the failure.
  await open(page, routes.Administration);
  const neutral = page.locator('main .ef-status-lozenge[data-state="unknown"]');
  expect(await neutral.count()).toBeGreaterThan(0);
  for (const bg of await neutral.evaluateAll((ls) => ls.map((l) => getComputedStyle(l).backgroundColor.replace(/\s/g, '')))) {
    expect([rgb(palette.amber), rgb(palette.coral), rgb(palette.teal)]).not.toContain(bg);
  }
  const amberOwners = await page.evaluate((amber) => [...document.querySelectorAll('body *')].filter((e) => getComputedStyle(e).backgroundColor.replace(/\s/g, '') === amber).map((e) => e.getAttribute('data-state')), rgb(palette.amber));
  expect(amberOwners).toEqual(['attention']);
  expect(await paintedColours(page)).not.toContain(rgb(palette.coral));
  await loadFailed(page);
  expect(await paintedColours(page)).toContain(rgb(palette.coral));
});

test('UI-016-O18', async ({ page }) => {
  // Not a greyscale interface at any viewport: the identity colours are painted at every size.
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await open(page, routes.Administration);
    const colours = await paintedColours(page);
    const identity = Object.values(palette).filter((hex) => colours.includes(rgb(hex)));
    expect(identity.length, `${vp.id}: ${identity.join(', ')}`).toBeGreaterThanOrEqual(4);
    expect(colours.filter(saturated).length, vp.id).toBeGreaterThan(2);
  }
});
