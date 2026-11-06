// Owner: integration. Opening the built application in the browser specs: the served build (dist/, at /dist/ under
// playwright.config.ts) or CAUSA_APP_URL. Waits for the engine's first view, never for a fixed time.
import { expect } from '@playwright/test';

export const appUrl = (route = '#/investigations') => `${process.env.CAUSA_APP_URL || '/dist/'}${route}`;

/// The engine has started: the loading status is gone and the engine wrote the document title.
export const started = async (page) => {
  await expect(page.getByRole('status').filter({ hasText: 'Loading Causa…' })).toBeHidden({ timeout: 30000 });
  await expect(page).toHaveTitle(/· Causa$/);
};

/// A fresh page load at a route (a goto that differs only in its hash would not reload the application).
export const load = async (page, route, options = {}) => {
  if (page.url() !== 'about:blank') await page.goto('about:blank');
  await page.goto(appUrl(route), options);
};

export const open = async (page, route) => {
  await load(page, route);
  await started(page);
};

/// The primary navigation shown at this viewport (the persistent column, or the compact menu, opened).
export const primary = async (page) => {
  const persistent = page.locator('nav.ef-navigation-shell__persistent');
  if (await persistent.isVisible()) return persistent;
  const menu = page.locator('details.ef-navigation-shell__compact');
  if (!(await menu.evaluate((d) => d.open))) await menu.locator('summary').click();
  return menu.locator('nav');
};

export const viewports = [
  { id: 'mobile', width: 390, height: 844 },
  { id: 'ipad-portrait', width: 820, height: 1180 },
  { id: 'ipad-landscape', width: 1180, height: 820 },
  { id: 'desktop', width: 1440, height: 900 },
];

export const destinations = ['Investigations', 'Administration', 'Settings'];
export const routes = { Investigations: '#/investigations', Administration: '#/administration', Settings: '#/settings' };

/// A catalog investigation route and a working-copy one (the route states the workspace; nothing is read).
export const catalogInvestigation = '#/investigation/workspaces%2Fdemo%2Fhistorical-v1/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview';
export const workingCopyInvestigation = '#/investigation/workspaces%2Fdemo%2Fcausa-app-r10/6f1b3c1e-8d7a-4c55-9a51-2f0b1d7e9c11/overview';

/// Every computed colour the page paints (text, background, borders, outlines, SVG fills and strokes).
export const paintedColours = (page) => page.evaluate(() => {
  const seen = new Set();
  for (const el of document.querySelectorAll('body, body *')) {
    // Only what is rendered: hidden fallbacks (the load failure, closed menus) paint nothing.
    if (!el.getClientRects().length) continue;
    const s = getComputedStyle(el);
    if (s.visibility === 'hidden') continue;
    for (const c of [s.color, s.backgroundColor, s.borderTopColor, s.borderRightColor, s.borderBottomColor, s.borderLeftColor, s.outlineColor, s.fill, s.stroke]) if (c) seen.add(c.replace(/\s/g, ''));
  }
  return [...seen];
});
export const rgb = (hex) => `rgb(${[1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)).join(',')})`;
export const palette = { navy: '#14283F', teal: '#087F8C', amber: '#E9A23B', coral: '#D66B58', ivory: '#F5F3ED' };
