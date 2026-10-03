import { test, expect } from "@playwright/test";

test("public deployment cannot select or invoke the client environment", async ({ page, request }) => {
    test.skip(!!process.env.CLIENT_E2E, "Public deployment check");
    await page.addInitScript(() => sessionStorage.setItem("rag-environment-mode", "client"));
    await page.goto("/");
    await expect(page.getByLabel("Environment mode")).toHaveValue("demo");
    await expect(page.getByLabel("Environment mode").locator('option[value="client"]')).toHaveCount(0);
    for (const method of ["GET", "POST"]) {
        const response = await request.fetch("/api/client/queries", { method, data: {} });
        expect(response.status()).toBe(403);
        expect(await response.json()).toMatchObject({ code: "CLIENT_DISABLED" });
    }
});
