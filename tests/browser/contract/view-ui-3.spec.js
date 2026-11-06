// Owner: ui-3. Renders the ui-3 analysis-section fragments (web/screens/workspace-*.html) with the real Limen 0.9.1
// browser kernel over an in-page transport that returns a projection of the J2 shape (keys exactly as
// Views/Workspace/*.fs and Views/Graph.fs emit them; the .NET test "Every key the ui-3 fragments bind is emitted by the
// views" checks the other direction), then checks the accessible names the governed journeys use and that events
// reach the engine. Limen throws on a missing data-text/data-bind key, so a projection that drifts from the fragments
// fails here.
import { readFileSync } from "node:fs";
import { expect, test } from "@playwright/test";

const fragments = ["workspace-overview", "workspace-causal-analysis", "workspace-evidence", "workspace-hypotheses", "workspace-timeline", "workspace-report"]
  .map((f) => readFileSync(new URL(`../../../web/screens/${f}.html`, import.meta.url), "utf8"))
  .join("\n");

const e1 = "handshake_failure entries in the gateway log", e2 = "database CPU at 40%";
const h1 = "The TLS certificate expired", h2 = "The database is saturated";
const r1 = "handshake failures match the error window", r2 = "the database was idle";
const action = (key, offered, disabled = false, reason = "") => ({ [`${key}.offered`]: offered, [`${key}.disabled`]: disabled, [`${key}.reason`]: reason });

const projection = {
  "screen.investigation.overview": true, "screen.investigation.causal-analysis": true, "screen.investigation.evidence": true,
  "screen.investigation.hypotheses": true, "screen.investigation.timeline": true,
  // Overview.fs
  "investigation.overview.loaded": true, "investigation.overview.missing": false, "investigation.overview.notLoaded": "This investigation's content is not loaded yet.",
  "investigation.overview.title": "Checkout failures", "investigation.overview.problem": "Checkout payments fail since 09:00",
  "investigation.overview.created": "2026-11-06T12:01:00Z", "investigation.overview.state": "active", "investigation.overview.version": "9",
  "investigation.overview.hypothesisCount": "2", "investigation.overview.evidenceCount": "2", "investigation.overview.observations.empty": false,
  "investigation.overview.observations": [{ id: "o3", text: "Error rate rose to 12% at 09:02", hasSource: false, source: "", hasObservedAt: false, observedAt: "" }],
  "investigation.observation.text": "", "investigation.observation.source": "", ...action("investigation.observation.record", true),
  // Conclusions.fs
  "investigation.conclusions.empty": false,
  "investigation.conclusions.items": [{ id: "c1", statement: "The expired certificate is the leading explanation", hasResidual: true, residual: "The renewal log is incomplete", version: "11" }],
  "investigation.conclusion.statement": "", ...action("investigation.conclusion.record", false, true),
  // Hypotheses.fs
  "investigation.hypotheses.loaded": true, "investigation.hypotheses.missing": false, "investigation.hypotheses.notLoaded": "", "investigation.hypotheses.empty": false,
  "investigation.hypotheses.items": [
    { id: "h1", statement: h1, state: "proposed", hasExplanation: true, explanation: "Handshakes fail", hasLinks: true, linkCount: 2, links: `supports: ${e1} — ${r1}\ncontradicts: ${e2} — ${r2}` },
    { id: "h2", statement: h2, state: "proposed", hasExplanation: false, explanation: "", hasLinks: false, linkCount: 0, links: "" }],
  "investigation.hypothesis.statement": "", "investigation.hypothesis.explanation": "", ...action("investigation.hypothesis.record", true),
  // Evidence.fs
  "investigation.evidence.loaded": true, "investigation.evidence.missing": false, "investigation.evidence.notLoaded": "", "investigation.evidence.empty": false,
  "investigation.evidence.items": [
    { id: "e1", hasLabel: false, label: "No label recorded", content: e1, hasSource: true, source: "https://logs.example.org/gateway/0900", hasUrl: true, url: "https://logs.example.org/gateway/0900", category: "log", hasCapturedAt: false, capturedAt: "", hasLinks: true, linkCount: 1, links: `supports: ${h1} — ${r1}` },
    { id: "e2", hasLabel: false, label: "No label recorded", content: e2, hasSource: true, source: "metrics dashboard", hasUrl: false, url: "", category: "log", hasCapturedAt: false, capturedAt: "", hasLinks: true, linkCount: 1, links: `contradicts: ${h1} — ${r2}` }],
  "investigation.evidence.label": "", "investigation.evidence.source": "", "investigation.evidence.url": "", "investigation.evidence.content": "",
  ...action("investigation.evidence.record", true),
  // Links.fs
  "investigation.links.loaded": true,
  "investigation.links.evidenceOptions": [{ id: "e1", label: e1, selected: false }, { id: "e2", label: e2, selected: false }],
  "investigation.links.hypothesisOptions": [{ id: "h1", label: h1, selected: false }, { id: "h2", label: h2, selected: false }],
  "investigation.links.evidenceUnchosen": true, "investigation.links.hypothesisUnchosen": true,
  "investigation.link.evidence": "", "investigation.link.hypothesis": "", "investigation.link.kind": "", "investigation.link.rationale": "",
  "investigation.link.kind.supports": false, "investigation.link.kind.contradicts": false, "investigation.link.kind.consistent-with": false, "investigation.link.kind.inconclusive": false,
  "investigation.links.history.empty": false,
  "investigation.links.history": [
    { id: "r1", evidence: e1, kind: "supports", hypothesis: h1, rationale: r1, current: true, status: "current" },
    { id: "r2", evidence: e2, kind: "contradicts", hypothesis: h1, rationale: r2, current: true, status: "current" }],
  ...action("investigation.link.relate", true), ...action("investigation.link.reassess", true),
  // Timeline.fs
  "investigation.timeline.loaded": true, "investigation.timeline.missing": false, "investigation.timeline.notLoaded": "", "investigation.timeline.empty": false,
  "investigation.timeline.items": [
    { id: "3", version: "3", eventType: "ObservationRecorded", at: "2026-11-06T12:03:00Z", actor: "investigator", summary: "Observation recorded: Error rate rose to 12% at 09:02" },
    { id: "8", version: "8", eventType: "EvidenceRelationshipRecorded", at: "2026-11-06T12:08:00Z", actor: "investigator", summary: `Assessment recorded as supports: ${e1} supports ${h1}. Rationale: ${r1}` }],
  // Graph.fs
  "investigation.causal.loaded": true, "investigation.causal.missing": false, "investigation.causal.notLoaded": "",
  "investigation.causal.relationships.empty": false,
  "investigation.causal.relationships": [
    { id: "r1", evidence: e1, kind: "supports", hypothesis: h1, rationale: r1, current: true, status: "current" },
    { id: "r2", evidence: e2, kind: "contradicts", hypothesis: h1, rationale: r2, current: true, status: "current" }],
  "investigation.causal.causes.empty": true, "investigation.causal.causes": [],
  // Report.fs
  "screen.investigation.report": true,
  "investigation.report.loaded": true, "investigation.report.missing": false, "investigation.report.notLoaded": "",
  "investigation.report.title": "Checkout failures", "investigation.report.id": "00000000-0000-4000-8000-000000000001",
  "investigation.report.state": "active", "investigation.report.version": "9", "investigation.report.asOf": "2026-11-06T12:09:00Z",
  "investigation.report.sections": [
    { id: "Problem", heading: "Problem", empty: false, body: "Checkout failures\nCheckout payments fail since 09:00\nState: active", sources: 1 },
    { id: "Conclusions", heading: "Conclusions", empty: true, body: "", sources: 0 }],
  "investigation.report.uncertainties.empty": false,
  "investigation.report.uncertainties": [{ id: "0", text: "Cause not confirmed: Checkout payments fail" }],
};

const page = (state) => `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>ui-3 sections</title>
<link rel="stylesheet" href="/node_modules/@echelon-foundry/design-system/dist/all.css">
<link rel="stylesheet" href="/web/screens/workspace-overview.css"><link rel="stylesheet" href="/web/screens/workspace-report.css"></head>
<body class="causa"><main class="causa-main">${fragments}</main>
<script type="module">
import { BrowserKernel } from "/node_modules/@echelon-foundry/limen/dist/index.js";
let view = ${JSON.stringify(state)};
// As Links.keys does: the chosen kind checks its option, and the chosen evidence and hypothesis select their rows.
const reflect = (v) => {
  const kind = v["investigation.link.kind"], ev = v["investigation.link.evidence"], hy = v["investigation.link.hypothesis"];
  for (const k of ["supports", "contradicts", "consistent-with", "inconclusive"]) v["investigation.link.kind." + k] = kind === k;
  v["investigation.links.evidenceOptions"] = v["investigation.links.evidenceOptions"].map((r) => ({ ...r, selected: r.id === ev }));
  v["investigation.links.hypothesisOptions"] = v["investigation.links.hypothesisOptions"].map((r) => ({ ...r, selected: r.id === hy }));
  v["investigation.links.evidenceUnchosen"] = ev === "";
  v["investigation.links.hypothesisUnchosen"] = hy === "";
  return v;
};
window.__events = []; window.__errors = [];
const transport = { async start() {}, async dispatch(m) {
  if (m.kind === "Event") { window.__events.push({ name: m.event.name, key: m.event.key, value: m.event.value });
    if (m.event.value !== undefined) view = reflect({ ...view, [m.event.name]: m.event.value }); }
  return { view, effects: [], cancellations: [] }; } };
const kernel = new BrowserKernel(transport, document, { report(e) { if (e.kind === "BridgeError") window.__errors.push(e.phase + ": " + e.detail); } });
await kernel.start(); window.__status = kernel.status;
</script></body></html>`;

test("renders the projection", async ({ page: p }) => {
  const errors = [];
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/__ui3__.html", (route) => route.fulfill({ contentType: "text/html", body: page(projection) }));
  await p.goto("/__ui3__.html");
  await expect.poll(() => p.evaluate(() => window.__status)).toBe("running");
  expect(await p.evaluate(() => window.__errors)).toEqual([]);

  // Overview: problem statement, state, observation, conclusion from history; the observation form by catalog names.
  await expect(p.getByText("Checkout payments fail since 09:00", { exact: true })).toBeVisible();
  await expect(p.getByText("Error rate rose to 12% at 09:02").first()).toBeVisible();
  await expect(p.getByText("The expired certificate is the leading explanation")).toBeVisible();
  await expect(p.getByLabel("Observation text", { exact: true })).toBeVisible();
  await expect(p.getByLabel("Observation source reference", { exact: true })).toBeVisible();
  await expect(p.getByRole("button", { name: "Record an observation", exact: true })).toBeEnabled();
  // INV-032: not offered by the engine, so not rendered.
  await expect(p.getByRole("button", { name: "Record a conclusion", exact: true })).toHaveCount(0);
  await expect(p.getByLabel("Conclusion statement", { exact: true })).toHaveCount(0);

  // Hypotheses: J2-02 names; J2-05 the hypothesis card lists both evidence items and the kinds.
  await expect(p.getByLabel("Hypothesis statement", { exact: true })).toBeVisible();
  await expect(p.getByRole("button", { name: "Record a hypothesis", exact: true })).toBeEnabled();
  const hypothesisCard = p.getByRole("article").filter({ hasText: h1 }).filter({ hasText: "Hypothesis ·" });
  await expect(hypothesisCard.getByText(e1).first()).toBeVisible();
  await expect(hypothesisCard.getByText(e2).first()).toBeVisible();
  await expect(hypothesisCard.getByText(/contradicts/).first()).toBeVisible();

  // Evidence: J2-03 names; the URL is a link only for the safe http(s) source.
  for (const name of ["Evidence label", "Evidence source reference", "Evidence source URL", "Evidence original content"])
    await expect(p.getByLabel(name, { exact: true })).toBeVisible();
  await expect(p.getByRole("button", { name: /record evidence|add evidence/i }).first()).toBeEnabled();
  const evidenceCard = p.getByRole("article").filter({ hasText: e1 }).filter({ hasText: "Category" });
  await expect(evidenceCard.getByText(h1).first()).toBeVisible();
  await expect(evidenceCard.getByText(/supports/).first()).toBeVisible();
  await expect(evidenceCard.getByRole("link", { name: "https://logs.example.org/gateway/0900" })).toHaveAttribute("href", "https://logs.example.org/gateway/0900");
  await expect(p.getByRole("article").filter({ hasText: e2 }).filter({ hasText: "Category" }).getByRole("link")).toHaveCount(0);

  // Links: J2-04 controls by role and name; choosing sends the engine the chosen values.
  await p.getByRole("combobox", { name: /evidence/i }).first().selectOption({ label: e2 });
  await p.getByRole("combobox", { name: /hypothes/i }).first().selectOption({ label: h1 });
  const group = p.getByRole("group", { name: /evidence relationship kind/i });
  await expect(group.getByRole("radio")).toHaveCount(4);
  for (const k of ["supports", "contradicts", "consistent-with", "inconclusive"]) await expect(group.getByLabel(k, { exact: true })).not.toBeChecked();
  await group.getByLabel("contradicts", { exact: true }).check();
  await expect(group.getByLabel("contradicts", { exact: true })).toBeChecked();
  await expect(p.getByRole("combobox", { name: /evidence/i }).first()).toHaveValue("e2");
  await expect(p.getByRole("combobox", { name: /hypothes/i }).first()).toHaveValue("h1");
  await p.getByLabel("Evidence relationship rationale", { exact: true }).fill("why");
  await p.getByRole("button", { name: "Link evidence to a hypothesis", exact: true }).click();
  await p.getByRole("button", { name: "Reassess", exact: true }).click();
  await expect.poll(() => p.evaluate(() => window.__events)).toEqual(expect.arrayContaining([
    { name: "investigation.link.evidence", value: "e2" },
    { name: "investigation.link.hypothesis", value: "h1" },
    { name: "investigation.link.kind", value: "contradicts" },
    { name: "investigation.link.rationale", value: "why" },
    { name: "investigation.link.relate" },
    { name: "investigation.link.reassess" },
  ]));

  // Causal Analysis and Timeline: J2-08 content.
  await expect(p.getByRole("list", { name: "Evidence and hypothesis relationships" }).getByText(r2)).toBeVisible();
  await expect(p.getByRole("list", { name: "Investigation events" }).getByText(/Observation recorded: Error rate/)).toBeVisible();
  // Report (UI-009, J2-10): an article named as a report holding the title, with a print presentation.
  const report = p.getByRole("article", { name: /report/i });
  await expect(report).toContainText("Checkout failures");
  await expect(report).toContainText("Cause not confirmed: Checkout payments fail");
  await expect(report.getByText("Use your browser's print command", { exact: false })).toBeVisible();
  await p.emulateMedia({ media: "print" });
  await expect(report.getByRole("heading", { name: /Report: Checkout failures/ })).toBeVisible();
  await expect(report.getByText("Use your browser's print command", { exact: false })).toBeHidden();
  await p.emulateMedia({ media: "screen" });

  expect(errors).toEqual([]);
  expect(await p.evaluate(() => window.__errors)).toEqual([]);
});
