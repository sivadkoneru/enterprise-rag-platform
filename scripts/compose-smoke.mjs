import { execFileSync } from "node:child_process";
import { mkdtempSync, writeFileSync, rmSync, mkdirSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { randomBytes, createHash } from "node:crypto";

const directory = mkdtempSync(join(tmpdir(), "rag-compose-smoke-"));
const key = randomBytes(32).toString("hex");
const environment = join(directory, ".env.client");
writeFileSync(environment, `RAG_API_KEY=${key}\nCLIENT_DOCUMENTS_PATH=${resolve("src/Rag.Playground/tests/fixtures")}\nLLM_PROVIDER=deterministic\nPLAYGROUND_PORT=0\n`, { mode: 0o600 });
const override = join(directory, "smoke.yml");
writeFileSync(override, 'services:\n  api:\n    ports: ["127.0.0.1:0:8080"]\n');
const project = `rag-smoke-${process.pid}`;
const compose = (...args) => execFileSync("docker", ["compose", "--project-name", project, "--env-file", environment, "-f", "compose.client.yml", "-f", override, ...args], { encoding: "utf8", stdio: ["ignore", "pipe", "inherit"], env: { ...process.env, RAG_CLIENT_ENV_FILE: environment, COMPOSE_PROFILES: "local-storage" } });
let apiOrigin = "";
const api = async (path, body) => {
  const response = await fetch(`${apiOrigin}/api/v1/${path}`, { method: body ? "POST" : "GET", headers: { "X-API-Key": key, "Content-Type": "application/json" }, body: body ? JSON.stringify(body) : undefined });
  if (!response.ok) throw new Error(`Smoke request failed: ${path}, HTTP ${response.status}`);
  return response.headers.get("content-type")?.includes("event-stream") ? response.text() : response.json();
};
try {
  compose("up", "--build", "--wait", "--wait-timeout", "240", "-d");
  apiOrigin = `http://${compose("port", "api", "8080").trim()}`;
  for (const [service, port] of [["api", "8080"], ["playground", "3000"]]) {
    if (!compose("port", service, port).startsWith("127.0.0.1:")) throw new Error(`${service} is not bound to loopback`);
  }
  for (let attempt = 0; attempt < 60; attempt++) {
    if (await fetch(`${apiOrigin}/health`).then(r => r.ok).catch(() => false)) break;
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  const corpus = await api("corpora", { name: "Smoke corpus" });
  const profile = await api(`corpora/${corpus.id}/profiles`, { name: "Smoke profile", strategy: "recursive", embeddingDimensions: 1536 });
  const job = await api(`profiles/${profile.id}/ingestions`, { sources: ["/documents/client-policy.md"] });
  let complete = false;
  for (let attempt = 0; attempt < 120; attempt++) {
    const status = await api(`jobs/${job.id}`);
    if (status.status === "complete") { complete = true; break; }
    if (status.status === "failed") throw new Error("Ingestion failed");
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  if (!complete) throw new Error("Ingestion timed out");
  compose("restart", "api");
  apiOrigin = `http://${compose("port", "api", "8080").trim()}`;
  for (let attempt = 0; attempt < 60; attempt++) {
    if (await fetch(`${apiOrigin}/health`).then(r => r.ok).catch(() => false)) break;
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  const profiles = await api(`corpora/${corpus.id}/profiles`);
  if (!profiles.some(item => item.id === profile.id && item.status === "ready")) throw new Error("Restart lost the profile");
  const result = await api("queries", { question: "What is the refund policy?", corpusId: corpus.id, profileId: profile.id, minRelevance: 0 });
  if (!result.includes("event: result") || !result.includes("30 calendar days")) throw new Error("Query did not return indexed evidence");
  const durations = [];
  let failures = 0;
  async function worker() {
    for (let i = 0; i < 10; i++) {
      const started = performance.now();
      try {
        const response = await api("queries", { question: "What is the refund policy?", corpusId: corpus.id, profileId: profile.id, minRelevance: 0 });
        if (!response.includes("event: result")) throw new Error("Missing result");
        durations.push(performance.now() - started);
      } catch { failures++; }
    }
  }
  await Promise.all([worker(), worker()]);
  durations.sort((a, b) => a - b);
  const docker = JSON.parse(execFileSync("docker", ["info", "--format", "{{json .}}"], { encoding: "utf8" }));
  const diff = execFileSync("git", ["diff"], { encoding: "utf8" });
  const report = {
    schemaVersion: 1, measuredAt: new Date().toISOString(), commit: execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim(),
    trackedDiffSha256: createHash("sha256").update(diff).digest("hex"), workingTree: "implementation working tree, including uncommitted additions",
    model: "deterministic local; no external inference", deployment: "Docker Compose, MongoDB, Elasticsearch, one API instance",
    hardware: { dockerArchitecture: docker.Architecture, dockerCpus: docker.NCPU, dockerMemoryBytes: docker.MemTotal, dockerVersion: docker.ServerVersion },
    corpusSha256: createHash("sha256").update(readFileSync("src/Rag.Playground/tests/fixtures/client-policy.md")).digest("hex"),
    documents: profiles.find(item => item.id === profile.id).documentCount, chunks: profiles.find(item => item.id === profile.id).chunkCount,
    concurrency: 2, warmupQueries: 1, attempted: 20, succeeded: durations.length, failures,
    p50Ms: durations[Math.ceil(durations.length * 0.5) - 1] ?? null, p95Ms: durations[Math.ceil(durations.length * 0.95) - 1] ?? null,
    durationsMs: durations, limitations: "Small synthetic smoke workload, post-restart warm query path; not a capacity test or real-model latency claim. No SLA inferred."
  };
  mkdirSync("test-results", { recursive: true });
  writeFileSync("test-results/compose-load.json", JSON.stringify(report, null, 2) + "\n");
  if (failures) throw new Error("Load smoke contained failed queries");
  console.log("Compose ingest/query, restart persistence and 20-query load smoke passed.");
} finally {
  compose("down", "--volumes", "--remove-orphans");
  rmSync(directory, { recursive: true, force: true });
}
