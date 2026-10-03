import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { resolve } from "node:path";

const key = randomBytes(32).toString("hex");
const apiUrl = "http://127.0.0.1:5088";
const fixture = resolve("tests/fixtures/client-policy.md");
const api = spawn("dotnet", [resolve("../Rag.Api/bin/Release/net10.0/Rag.Api.dll")], {
  cwd: resolve("../Rag.Api/bin/Release/net10.0"), stdio: "inherit",
  env: { ...process.env, ASPNETCORE_URLS: apiUrl, RAG_API_KEY: key, LLM_PROVIDER: "deterministic", DOC_STORE: "memory", JOB_STORE: "memory", VECTOR_STORE: "memory", LOCAL_SOURCE_ALLOWED_ROOTS: resolve("tests/fixtures") },
});
let browser;
try {
  let ready = false;
  for (let attempt = 0; attempt < 60; attempt++) {
    if (api.exitCode !== null) throw new Error("API exited before readiness");
    if (await fetch(`${apiUrl}/health`).then(r => r.ok).catch(() => false)) { ready = true; break; }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  if (!ready) throw new Error("API startup timed out");
  if ((await fetch(`${apiUrl}/api/v1/corpora`)).status !== 401) throw new Error("API key enforcement failed");
  if ((await fetch(`${apiUrl}/api/v1/corpora`, { headers: { "X-API-Key": "wrong-key" } })).status !== 401) throw new Error("Wrong API key was accepted");
  const configuration = await fetch(`${apiUrl}/api/v1/configuration`, { headers: { "X-API-Key": key } });
  if (!configuration.ok || (await configuration.text()).includes(key)) throw new Error("Configuration response exposed the API key");
  browser = spawn("npx", ["playwright", "test", "client-workflow.spec.ts"], {
    stdio: "inherit", env: { ...process.env, CLIENT_E2E: "1", CLIENT_FIXTURE_PATH: fixture, RAG_PLAYGROUND_MODE: "private-live", RAG_PLAYGROUND_ORIGIN: "http://127.0.0.1:3100", RAG_API_URL: apiUrl, RAG_API_KEY: key },
  });
  process.exitCode = await new Promise(resolve => browser.on("exit", code => resolve(code ?? 1)));
} finally { browser?.kill(); api.kill(); }
