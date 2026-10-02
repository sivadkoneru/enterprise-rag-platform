import { execFileSync, spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  addComposeProfile,
  applyStorageTopology,
  configCatalog,
  createAppSettings,
  createDefaultFormValues,
  createEnvironmentFile,
  validateConfiguration,
} from "@/lib/integrations/catalog";

describe("client integration configuration catalog", () => {
  it("covers the runtime, model, retrieval, query, storage, vector, and source options", () => {
    expect(configCatalog.schemaVersion).toBe(1);
    expect(configCatalog.groups.map((group) => group.id)).toEqual([
      "runtime", "models", "retrieval", "query", "storage", "vectors", "sources",
    ]);
    for (const field of configCatalog.groups.flatMap((group) => group.fields)) {
      expect(field.environmentAliases.length > 0 || (field.section !== null && field.option !== null), field.id).toBe(true);
      expect(field.appliesTo, field.id).toBeTruthy();
      expect(field.validation, field.id).toBeTruthy();
      expect(["restart", "reindex"]).toContain(field.changeEffect);
    }
  });

  it("keeps supplied secret values out of environment and .NET settings downloads", () => {
    const values = applyStorageTopology(createDefaultFormValues(), "local");
    for (const field of configCatalog.groups.flatMap((group) => group.fields)) {
      if (field.secret) values[field.id] = "DO_NOT_EXPORT_secret-sentinel";
    }

    const environment = createEnvironmentFile(values);
    const settings = JSON.parse(createAppSettings(values)) as Record<string, Record<string, unknown>>;
    expect(environment).not.toContain("DO_NOT_EXPORT_secret-sentinel");
    expect(settings.Api.ApiKey).toBeNull();
    expect(settings.Llm.ApiKey).toBeNull();
    expect(settings.Reranker.ApiKey).toBeNull();
    expect(settings.DocumentStore.ConnectionString).toBeNull();
    expect(settings.VectorStore.Password).toBeNull();
    expect(environment).toContain("RAG_API_KEY=");
  });

  it("selects local and external storage profiles in generated environment values", () => {
    const initial = createDefaultFormValues();
    expect(initial["compose-profiles"]).toBe("local-storage");

    const local = applyStorageTopology(initial, "local");
    expect(local["compose-profiles"]).toBe("local-storage");
    expect(local["document-connection"]).toBe("mongodb://mongo:27017");
    expect(local["vector-endpoint"]).toBe("http://elasticsearch:9200");

    const external = applyStorageTopology(local, "external");
    expect(external["compose-profiles"]).toBe("");
    expect(external["document-connection"]).toBe("");
    expect(external["vector-endpoint"]).toBe("");
    expect(createEnvironmentFile(external)).toContain("COMPOSE_PROFILES=\n");

    const withEmulator = addComposeProfile(external, "s3-emulator", false);
    expect(withEmulator["compose-profiles"]).toBe("s3-emulator");
  });

  it("blocks invalid dimensions, numeric values, and chunk overlaps", () => {
    const values = applyStorageTopology(createDefaultFormValues(), "local");
    values["embedding-dimensions"] = "768";
    expect(validateConfiguration(values)).toContain("Embedding and Elasticsearch vector dimensions must match.");

    values["embedding-dimensions"] = values["vector-dimensions"]!;
    values["chunk-overlap"] = values["chunk-size"]!;
    expect(validateConfiguration(values)).toContain("Chunk overlap must be smaller than chunk size.");

    values["chunk-overlap"] = "120";
    values["vector-dimensions"] = "not-a-number";
    expect(validateConfiguration(values).some((error) => error.includes("Dimensions must be a valid number"))).toBe(true);
  });

  it("round-trips apostrophes, newlines, hashes, and backslashes through Docker Compose when available", () => {
    const dockerAvailable = spawnSync("docker", ["compose", "version"], { stdio: "ignore" }).status === 0;
    if (!dockerAvailable) return;

    const values = createDefaultFormValues();
    const prompt = "The assistant's first line\nsecond line with # text; backslash \\ stays";
    values["system-prompt"] = prompt;
    const tempDirectory = mkdtempSync(join(tmpdir(), "rag-compose-env-"));
    try {
      writeFileSync(join(tempDirectory, "compose.yml"), "services:\n  probe:\n    image: busybox:1.36.1\n    env_file: generated.env\n");
      writeFileSync(join(tempDirectory, "generated.env"), createEnvironmentFile(values));
      const output = execFileSync("docker", ["compose", "-f", join(tempDirectory, "compose.yml"), "config", "--format", "json"], { cwd: tempDirectory, encoding: "utf8" });
      const parsed = JSON.parse(output) as { services: { probe: { environment: Record<string, string> } } };
      expect(parsed.services.probe.environment.LLM_SYSTEM_PROMPT).toBe(prompt);
    } finally {
      rmSync(tempDirectory, { recursive: true, force: true });
    }
  });

  it("keeps the downloadable Compose file identical to the project template", () => {
    const appDirectory = resolve(dirname(fileURLToPath(import.meta.url)), "..");
    const projectDirectory = resolve(appDirectory, "../..");
    const rootCompose = readFileSync(join(projectDirectory, "compose.client.yml"), "utf8");
    const downloadedCompose = readFileSync(join(appDirectory, "public", "compose.client.yml"), "utf8");
    expect(downloadedCompose).toBe(rootCompose);
  });
});
