// Owner: ui. UI-033: traversal between related records by stable identifiers.
import { test, expect } from '@playwright/test';
import { investigationWithContent, section } from './workspace-data.mjs';

test('UI-033-O1', async ({ page }) => {
  // Evidence -> hypothesis -> evidence through the actual links, and a timeline event -> its record.
  const v = await investigationWithContent(page);
  await section(page, 'Evidence');
  await page.getByRole('article').filter({ hasText: v.e1 }).getByRole('link', { name: 'Details' }).click();
  const inspector = page.getByRole('complementary');
  await inspector.getByRole('link', { name: `supports: ${v.h1}` }).click();
  await expect(page.getByRole('complementary').filter({ hasText: v.h1 })).toBeVisible();
  await page.getByRole('complementary').getByRole('link', { name: `contradicts: ${v.e2}` }).click();
  await expect(page.getByRole('complementary').filter({ hasText: v.e2 })).toBeVisible();
});

test('UI-033-O2', async ({ page }) => {
  // Links carry the record's id; an unlinked record says so; an id not in this investigation is reported absent.
  const v = await investigationWithContent(page);
  await section(page, 'Overview');
  const href = await page.getByRole('article').filter({ hasText: v.observation }).getByRole('link', { name: 'Details' }).getAttribute('href');
  expect(href).toMatch(/\?record=[0-9a-f-]{36}$/);
  await page.goto(page.url().replace(/\/overview.*$/, '/overview') + href.slice(href.indexOf('?')));
  await expect(page.getByRole('complementary').getByText('No linked records.')).toBeVisible();
  await page.goto(page.url().replace(/\?record=.*$/, '?record=00000000-0000-4000-8000-000000000999'));
  await expect(page.getByText('The selected record is not part of this investigation.')).toBeVisible();
});
