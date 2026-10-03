import { chromium } from "@playwright/test";
import { spawn } from "node:child_process";
import { mkdir, copyFile, rm } from "node:fs/promises";
import { resolve } from "node:path";

const output = resolve("../../docs/demo/portfolio");
await mkdir(output, { recursive: true });
const server = spawn(process.execPath, ["scripts/start-production.mjs"], { stdio: "ignore", env: { ...process.env, PORT: "3119", HOSTNAME: "127.0.0.1", RAG_PLAYGROUND_MODE: "demo" } });
let browser;
try {
  for (let attempt = 0; attempt < 60; attempt++) {
    if (await fetch("http://127.0.0.1:3119").then(r => r.ok).catch(() => false)) break;
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: "reduce", recordVideo: { dir: `${output}/raw`, size: { width: 1440, height: 1000 } } });
  const page = await context.newPage();
  await page.goto("http://127.0.0.1:3119");
  await page.waitForTimeout(3000);
  await page.getByRole("button", { name: /Run Query/ }).click();
  await page.getByRole("button", { name: "Citation 1: show source" }).waitFor();
  await page.waitForTimeout(4000);
  await page.getByRole("button", { name: "Citation 1: show source" }).click();
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: `${output}/answer-evidence.png` });
  await page.waitForTimeout(5000);
  await page.getByRole("link", { name: "Retrieval", exact: true }).first().click();
  await page.getByRole("button", { name: "#5 · 99", exact: true }).click();
  await page.waitForTimeout(500);
  await page.evaluate(() => window.scrollTo(0, 350));
  await page.screenshot({ path: `${output}/retrieval-diagnosis.png` });
  await page.waitForTimeout(6000);
  await page.getByRole("link", { name: "Playground", exact: true }).first().click();
  await page.getByLabel("Ask a question").fill("What is the orbital period of Jupiter?");
  await page.getByRole("button", { name: /Run Query/ }).click();
  await page.getByText(/There is not enough supporting evidence/).waitFor();
  await page.screenshot({ path: `${output}/unsupported-question.png` });
  await page.waitForTimeout(6000);
  await context.close();
  await copyFile(await page.video().path(), `${output}/simulation.webm`);
  await rm(`${output}/raw`, { recursive: true, force: true });
} finally { await browser?.close(); server.kill(); }
