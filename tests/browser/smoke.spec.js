// Scaffold smoke suite: every page loads every resource it references and
// raises no script error. Extend it with the application's real journeys.
import { expect, test } from "@playwright/test";

const pages = ["src/kernel/index.html", "src/kernel/print.html"];

for (const path of pages) {
  test(`${path} loads every resource it references`, async ({ page }) => {
    const failures = [];
    page.on("response", (response) => {
      if (response.status() >= 400) failures.push(`${response.status()} ${response.url()}`);
    });
    page.on("requestfailed", (request) => failures.push(`failed ${request.url()}`));
    page.on("pageerror", (error) => failures.push(`script error: ${error.message}`));

    await page.goto(`/${path}`);
    await page.waitForLoadState("networkidle");

    expect(failures).toEqual([]);
  });
}

test("the application page renders its first control", async ({ page }) => {
  await page.goto("/src/kernel/index.html");
  await expect(page.getByRole("button", { name: "Ready" })).toBeVisible();
});
