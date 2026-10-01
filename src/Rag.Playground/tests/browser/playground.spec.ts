import { expect, test } from "@playwright/test";

test("query, citations, documents, clipboard and session history", async ({
    page,
    context,
}) => {
    await context.grantPermissions(["clipboard-read", "clipboard-write"]);
    const errors: string[] = [];
    const external: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    page.on("request", (request) => {
        if (
            request.url().startsWith("http") &&
            new URL(request.url()).hostname !== "127.0.0.1"
        )
            external.push(request.url());
    });
    await page.goto("/");
    await expect(page.getByText("Answers start with evidence.")).toBeVisible();
    await expect(page.getByText("42 documents", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Browse Corpus" }).click();
    await page
        .getByRole("textbox", { name: "Search corpus files" })
        .fill("enterprise");
    await page
        .getByRole("button", { name: /enterprise-agreements.md/ })
        .click();
    await expect(
        page.getByRole("heading", { name: "Enterprise Exceptions" }),
    ).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toBeHidden();
    await page.getByRole("button", { name: /Run Query/ }).click();
    await expect(
        page
            .getByText(
                "Customers may request a full refund within 30 calendar days of purchase.",
            )
            .first(),
    ).toBeVisible();
    await expect(page.locator("article.evidence-card")).toHaveCount(5);
    await expect(
        page.getByRole("button", { name: /Citation \d: show source/ }),
    ).toHaveCount(3);
    await page.getByRole("button", { name: "Citation 1: show source" }).click();
    await expect(page.locator("#source-chunk-128")).toHaveAttribute(
        "data-highlighted",
        "true",
    );
    await expect(page.locator("#source-chunk-128")).toBeFocused();
    await page
        .locator("#source-chunk-128")
        .getByRole("button", { name: "View document" })
        .click();
    await expect(page.getByRole("dialog")).toContainText("Refund Policy");
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toBeHidden();
    await page.getByRole("button", { name: "Copy answer" }).click();
    await expect(
        page.getByRole("button", { name: "Copied", exact: true }),
    ).toBeVisible();
    expect(await page.evaluate(() => navigator.clipboard.readText())).toContain(
        "[1] handbook.md",
    );
    await page.screenshot({
        path: "test-results/playground-desktop.png",
        fullPage: true,
    });
    await page.reload();
    await page.getByRole("button", { name: /Query history/ }).click();
    await page
        .getByRole("dialog")
        .getByRole("button", { name: /refund policy and what exceptions/ })
        .click();
    await expect(page.locator("article.evidence-card")).toHaveCount(5);
    await expect(page.getByLabel("Ask a question")).toHaveValue(
        "What is the company's refund policy and what exceptions apply?",
    );
    expect(errors).toEqual([]);
    expect(external).toEqual([]);
});

test("cancellation, failed stage and successful retry", async ({ page }) => {
    await page.goto("/");
    await page.getByRole("button", { name: /Run Query/ }).click();
    await page.getByRole("button", { name: "Cancel query" }).click();
    await expect(page.getByRole("status")).toContainText("Query canceled");
    await page.getByText("Demo controls", { exact: false }).click();
    await page
        .getByRole("checkbox", {
            name: "Simulate a recoverable provider timeout",
        })
        .check();
    await page.getByRole("button", { name: /Run Query/ }).click();
    await expect(
        page.getByRole("alert").filter({ hasText: "Query could not complete" }),
    ).toContainText("Simulated embedding timeout");
    await page.getByRole("button", { name: "Retry query" }).click();
    await expect(page.locator("article.evidence-card")).toHaveCount(5);
    await expect(
        page.getByText("Query could not complete", { exact: true }),
    ).toHaveCount(0);
});

test("unsupported question and settings affect results", async ({ page }) => {
    await page.goto("/");
    await page
        .getByLabel("Ask a question")
        .fill("What is the orbital period of Jupiter?");
    await page.getByRole("button", { name: /Run Query/ }).click();
    await expect(
        page.getByText(/There is not enough supporting evidence/),
    ).toBeVisible();
    await expect(
        page.getByRole("button", { name: /Citation \d: show source/ }),
    ).toHaveCount(0);
    await page.getByRole("button", { name: "Clear", exact: true }).click();
    await expect(page.getByText("Answers start with evidence.")).toBeVisible();
    await expect(
        page.getByRole("button", { name: /Run Query/ }),
    ).toBeDisabled();
    await page.getByLabel("Ask a question").fill("What is the refund policy?");
    await page.getByRole("switch", { name: "Reranker", exact: true }).click();
    await page.getByRole("slider", { name: "Top K" }).focus();
    await page.keyboard.press("Home");
    await page.getByRole("button", { name: /Run Query/ }).click();
    await expect(page.locator("article.evidence-card")).toHaveCount(1);
    await expect(
        page.getByRole("button", { name: /Reranking.*Skipped/ }),
    ).toBeVisible();
});

test("evaluation metrics, search and case inspection", async ({ page }) => {
    await page.goto("/evaluation");
    await expect(
        page.getByRole("heading", { name: "RAG Evaluation Dashboard" }),
    ).toBeVisible();
    await page
        .getByRole("combobox", { name: "Active strategy", exact: true })
        .selectOption("semantic");
    await page.getByPlaceholder("Question, ID, or gold section…").fill("q-001");
    await page
        .getByRole("button", {
            name: "How long does a customer have to request a full refund?",
            exact: true,
        })
        .click();
    await expect(page.getByRole("dialog")).toContainText("Expected answer");
    await expect(page.getByRole("dialog")).toContainText("Generated answer");
    await expect(page.getByRole("dialog")).toContainText("handbook.pdf");
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toBeHidden();
    await page
        .getByPlaceholder("Question, ID, or gold section…")
        .fill("no such evaluation question 123");
    await expect(page.getByText("No cases match these filters.")).toBeVisible();
    await page.getByRole("button", { name: "Reset filters" }).click();
    await page.getByRole("button", { name: "Next", exact: true }).click();
    await expect(page.getByText(/Showing 11/)).toBeVisible();
    await page.getByRole("button", { name: "Previous", exact: true }).click();
    await expect(page.getByText(/Showing 1–10/)).toBeVisible();
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({
        path: "test-results/evaluation-desktop.png",
        fullPage: true,
    });
});

test("retrieval chart selection, threshold and source inspection", async ({
    page,
}) => {
    await page.goto("/retrieval");
    await expect(
        page.getByRole("heading", { name: "Retrieval Score Distribution" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "#2 · 244", exact: true }).click();
    await expect(
        page.getByRole("button", { name: "#2 · 244", exact: true }),
    ).toHaveAttribute("aria-pressed", "true");
    await page.getByRole("button", { name: "View document" }).click();
    await expect(page.getByRole("dialog")).toContainText(
        "Enterprise Exceptions",
    );
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toBeHidden();
    await page.getByRole("slider", { name: "Minimum relevance" }).focus();
    await page.keyboard.press("End");
    await expect(
        page.getByText(/No supporting evidence qualifies/),
    ).toBeVisible();
    await page.screenshot({
        path: "test-results/retrieval-desktop.png",
        fullPage: true,
    });
});

test("architecture, about and responsive navigation", async ({ page }) => {
    await page.goto("/architecture");
    await page.getByRole("button", { name: /Inspect.*[Cc]hunk/ }).click();
    await expect(page.getByRole("dialog")).toContainText("Inputs");
    await expect(page.getByRole("dialog")).toContainText("Outputs");
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toBeHidden();
    await page.screenshot({
        path: "test-results/architecture-desktop.png",
        fullPage: true,
    });
    await page.getByRole("button", { name: "About this project" }).click();
    await expect(page.getByRole("dialog")).toContainText("Evaluation harness");
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toBeHidden();
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/");
    await expect(
        page.getByRole("button", { name: "Open navigation" }),
    ).toBeVisible();
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
    ).toBe(true);
    await page.getByRole("button", { name: "Toggle color theme" }).click();
    await expect(page.locator("html")).toHaveClass(/dark/);
    await page.screenshot({
        path: "test-results/playground-mobile-dark.png",
        fullPage: true,
    });
    await page.getByRole("button", { name: "Open navigation" }).click();
    await page
        .getByRole("dialog")
        .getByRole("link", { name: "Evaluation", exact: true })
        .click();
    await expect(
        page.getByRole("heading", { name: "RAG Evaluation Dashboard" }),
    ).toBeVisible();
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
    ).toBe(true);
    await page.setViewportSize({ width: 834, height: 1112 });
    await page.screenshot({
        path: "test-results/evaluation-tablet-dark.png",
        fullPage: true,
    });
});
