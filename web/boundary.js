// Owner: integration. The browser boundary: Limen's BrowserKernel over the F# engine compiled to .NET WebAssembly
// (dist/_framework, export Causa.Host.Host.Dispatch). Mechanism only: every decision is the engine's.
import { BrowserKernel } from "./limen/index.js";
import { DotnetWasmTransport } from "./limen/hosts/dotnet-wasm-transport.js";

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

// WASM-LOAD-005: never leave the loading status up indefinitely. A start that has not finished in this time is a
// failure the user can retry (the kernel never rejects; it reports through diagnostics).
const START_TIMEOUT_MS = 30000;
const kernel = new BrowserKernel(transport, document, diagnostics);
const timedOut = new Promise((resolve) => setTimeout(() => resolve("timeout"), START_TIMEOUT_MS));
const outcome = await Promise.race([kernel.start().then(() => "started", () => "failed"), timedOut]);
if (outcome === "timeout") console.error(`Causa did not start within ${START_TIMEOUT_MS / 1000} seconds.`);
if (outcome !== "started" || kernel.status !== "running") showFailure();
else document.documentElement.dataset.causaStartedMs = String(Math.round(performance.now()));
