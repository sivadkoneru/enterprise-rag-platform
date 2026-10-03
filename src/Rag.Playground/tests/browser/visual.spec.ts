import { test, expect } from "@playwright/test";

test("approved Linux screenshots preserve answer and mobile refusal states", async ({ page }) => {
    test.skip(!process.env.VISUAL, "Use the pinned Linux Playwright container for visual comparisons.");
    await page.clock.setFixedTime(new Date("2026-10-03T00:00:00Z"));
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.goto("/");
    await page.getByRole("button", { name: /Run Query/ }).click();
    await expect(page.getByRole("button", { name: "Citation 1: show source" })).toBeVisible();
    await page.getByRole("button", { name: "Citation 1: show source" }).click();
    await page.evaluate(() => window.scrollTo(0, 0));
    await expect(page).toHaveScreenshot("answer-evidence.png", { animations: "disabled", maxDiffPixelRatio: 0.002 });
    await page.getByLabel("Ask a question").fill("What is the stock price of the company?");
    await page.getByRole("button", { name: /Run Query/ }).click();
    await expect(page.getByRole("button", { name: /Citation \d: show source/ })).toHaveCount(0);
    await expect(page.getByText(/There is not enough supporting evidence/)).toBeVisible();
    await page.setViewportSize({ width: 390, height: 844 });
    await page.evaluate(() => window.scrollTo(0, 0));
    await expect(page.locator("section").filter({ has: page.getByRole("heading", { name: "Answer with source references", exact: true }) })).toHaveScreenshot("unsupported-mobile.png", { animations: "disabled", maxDiffPixelRatio: 0.002 });
});
