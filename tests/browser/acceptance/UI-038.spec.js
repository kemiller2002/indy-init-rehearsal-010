// Owner: ui. UI-038: conclusions are explained from typed records. The frozen contract has no command that records a
// conclusion (INV-032), so the conclusion comes from a stored causa-storage history (contract fixture a6747bd3: an SSO
// outage concluded as a confirmed cause, with supporting and contradicting assessments) written to the provider and
// opened through the application.
import { test, expect } from '@playwright/test';
import { connect, seedStoredHistory, openListed } from './workspace-data.mjs';

const statement = /^The SSO outage was caused by a stale signing-key cache/;

const openConcluded = async (page) => {
  const route = await seedStoredHistory('a6747bd3-2794-5ac7-83b8-5ea2a0c50f5e');
  await connect(page);
  await openListed(page, route);
  const conclusions = page.getByRole('list', { name: 'Conclusions' }).getByRole('article');
  // Nothing to explain is a failure here, never a pass.
  await expect(conclusions).toHaveCount(1, { timeout: 20000 });
  return conclusions.first();
};

test('UI-038-O1', async ({ page }) => {
  // The conclusion carries its explanation: its cause, the linked hypothesis, and the hypothesis's supporting and
  // conflicting evidence as recorded.
  const conclusion = await openConcluded(page);
  await expect(conclusion.locator('[data-requirement-id="INV-033"]')).toHaveText(statement);
  const explanation = conclusion.locator('[data-requirement-id="UI-038"]');
  await expect(explanation).toContainText('Cause (');
  await expect(explanation).toContainText('Single sign-on outage');
  await expect(explanation).toContainText(/Hypothesis .+: supporting evidence (?!none recorded)/);
  await expect(conclusion).toContainText('Residual uncertainty: The manual key load is not independently verified');
});

test('UI-038-O2', async ({ page }) => {
  // Every name in the explanation is a recorded record: no conflicting evidence is recorded for the cause's
  // hypothesis, and the explanation says "none recorded" rather than inventing any.
  const conclusion = await openConcluded(page);
  const explanation = conclusion.locator('[data-requirement-id="UI-038"]');
  await expect(explanation).toContainText('conflicting evidence none recorded');
  const text = await explanation.innerText();
  await page.getByRole('navigation', { name: 'Investigation sections' }).getByRole('link', { name: 'Evidence', exact: true }).click();
  const evidence = await page.getByRole('list', { name: 'Evidence', exact: true }).locator('.causa-record__text').allTextContents();
  expect(evidence.length).toBeGreaterThan(0);
  const named = text.match(/supporting evidence ([^.]+?); conflicting/)[1].split('; ');
  for (const n of named) expect(evidence, n).toContain(n);
});
