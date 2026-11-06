// Owner: integration. The browser boundary: Limen's BrowserKernel over the F# engine compiled to .NET WebAssembly
// (dist/_framework, export Causa.Host.Host.Dispatch). Mechanism only: every decision is the engine's.
import { BrowserKernel } from "./limen/index.js";
import { DotnetWasmTransport } from "./limen/hosts/dotnet-wasm-transport.js";
import { lifecycleCapability } from "./limen/capabilities/lifecycle/index.js";
import { scheduleCapability } from "./limen/capabilities/schedule/index.js";
import { filesCapability } from "./limen/capabilities/files/index.js";

// Provider reads are never answered from the browser's HTTP cache (api.github.com sends Cache-Control: private,
// max-age=60): a read-back or a head read must see the provider's current answer. Mechanism only.
const nativeFetch = globalThis.fetch.bind(globalThis);
globalThis.fetch = (input, init = {}) => {
  const method = String(init.method || (input instanceof Request ? input.method : "GET")).toUpperCase();
  return nativeFetch(input, method === "GET" ? { ...init, cache: "no-store" } : init);
};

// PERSIST-019: the engine says (data-leave-guard="on") while any investigation is not Saved; the boundary registers a
// beforeunload handler only then, so a saved tab stays eligible for the back/forward cache. Mechanism only.
const warnOnLeave = (event) => { event.preventDefault(); event.returnValue = ""; };
const app = document.querySelector(".causa-app");
let guarded = false;
const applyGuard = () => {
  const wanted = app?.getAttribute("data-leave-guard") === "on";
  if (wanted === guarded) return;
  guarded = wanted;
  if (wanted) window.addEventListener("beforeunload", warnOnLeave);
  else window.removeEventListener("beforeunload", warnOnLeave);
};
if (app) new MutationObserver(applyGuard).observe(app, { attributes: true, attributeFilter: ["data-leave-guard"] });

const loading = document.querySelector(".causa-loading");
const failed = document.getElementById("causa-load-failed");

// WASM-LOAD-005: a failed start replaces the loading status with the failure and a Retry that loads the page again.
const showFailure = () => {
  if (loading) loading.hidden = true;
  if (failed) failed.hidden = false;
};
document.getElementById("causa-retry")?.addEventListener("click", () => window.location.reload());

const diagnostics = {
  report(event) {
    // Bridge mechanism only; Limen never includes request headers or bodies in diagnostics.
    if (event.kind === "BridgeError") console.error(`Limen ${event.phase} error: ${event.detail}`);
  },
};

const transport = new DotnetWasmTransport({
  loadRuntime: () => import("./_framework/dotnet.js"),
  exportPath: ["Causa", "Host", "Host", "Dispatch"],
});

// WDL-001 (T24): Limen binds a workflow document (Forma portable workflow format) to <forma-workflow data-workflow>;
// the element takes documents only through its host API (forma-workflow-host/1), so the boundary hands each new
// document to load(). Mechanism only: the engine lays the graph out; nothing here reads or changes its meaning.
const workflowsLoaded = new WeakMap();
const syncWorkflow = (element) => {
  const text = element.getAttribute("data-workflow");
  if (!text || workflowsLoaded.get(element) === text) return;
  workflowsLoaded.set(element, text);
  customElements.whenDefined("forma-workflow")
    .then(() => element.load(text))
    .catch((error) => console.error(`The causal graph could not be drawn: ${error instanceof Error ? error.message : String(error)}`));
};
new MutationObserver((records) => {
  for (const record of records) {
    if (record.type === "attributes" && record.target.localName === "forma-workflow") syncWorkflow(record.target);
    for (const node of record.addedNodes) {
      if (node.nodeType !== Node.ELEMENT_NODE) continue;
      if (node.matches("forma-workflow[data-workflow]")) syncWorkflow(node);
      node.querySelectorAll("forma-workflow[data-workflow]").forEach(syncWorkflow);
    }
  }
}).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-workflow"] });

// WASM-LOAD-005: never leave the loading status up indefinitely. A start that has not finished in this time is a
// failure the user can retry (the kernel never rejects; it reports through diagnostics).
const START_TIMEOUT_MS = 30000;
// Optional capabilities the engine selects in its handshake: connectivity (SM-NETWORK), timers (reconciliation reads)
// and downloads (conflict export).
const capabilities = [lifecycleCapability(), scheduleCapability(), filesCapability()];
const kernel = new BrowserKernel(transport, document, diagnostics, { capabilities });
const timedOut = new Promise((resolve) => setTimeout(() => resolve("timeout"), START_TIMEOUT_MS));
const outcome = await Promise.race([kernel.start().then(() => "started", () => "failed"), timedOut]);
if (outcome === "timeout") console.error(`Causa did not start within ${START_TIMEOUT_MS / 1000} seconds.`);
if (outcome !== "started" || kernel.status !== "running") showFailure();
else document.documentElement.dataset.causaStartedMs = String(Math.round(performance.now()));
