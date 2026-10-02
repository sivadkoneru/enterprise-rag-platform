import type { ConfigurationCatalog, ConfigurationField, FieldPrimitive } from "@/lib/integrations/catalog";

export interface ReadinessCheck {
  id: string;
  status: "ready" | "unconfigured" | "unavailable";
  detail: string;
}

export interface ReadinessResult {
  ready: boolean;
  checks: ReadinessCheck[];
}

export interface CapabilityResult {
  reranker: boolean;
  hybrid: boolean;
  embeddingModel: string;
  embeddingDimensions: number;
  documentStore: string;
  vectorStore: string;
  jobStore: string;
  schemaVersion: number;
  vectorScoreKind: string;
  lexicalAlgorithm: string;
  tokenUsageKind: string;
  llmProvider: string;
  supportedStrategies: string[];
  defaultQuery: {
    topK: number;
    mode: string;
    reranker: boolean;
    minRelevance: number;
    neighbors: boolean;
    maxContextTokens: number;
  };
}

export interface ConfiguredField extends ConfigurationField {
  value: FieldPrimitive | null;
  configured: boolean;
}

export interface RuntimeConfiguration extends Omit<ConfigurationCatalog, "groups"> {
  groups: Array<Omit<ConfigurationCatalog["groups"][number], "fields"> & { fields: ConfiguredField[] }>;
}

export interface EndpointResult<T> {
  status: "connected" | "error";
  data?: T;
  message?: string;
}

export interface ClientStatus {
  checkedAt: string;
  readiness: EndpointResult<ReadinessResult>;
  capabilities: EndpointResult<CapabilityResult>;
  configuration: EndpointResult<RuntimeConfiguration>;
}

export interface IntegrationStatus {
  name: "embedding" | "chat" | "reranker";
  status: "healthy" | "failed" | "not-configured";
  message: string;
  latencyMs: number;
  details: Record<string, unknown>;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isReadiness(value: unknown): value is ReadinessResult {
  return isRecord(value)
    && typeof value.ready === "boolean"
    && Array.isArray(value.checks)
    && value.checks.every((check) => isRecord(check)
      && typeof check.id === "string"
      && (check.status === "ready" || check.status === "unconfigured" || check.status === "unavailable")
      && typeof check.detail === "string");
}

function isCapabilities(value: unknown): value is CapabilityResult {
  return isRecord(value)
    && typeof value.reranker === "boolean"
    && typeof value.hybrid === "boolean"
    && typeof value.embeddingModel === "string"
    && typeof value.embeddingDimensions === "number"
    && typeof value.documentStore === "string"
    && typeof value.vectorStore === "string"
    && typeof value.jobStore === "string"
    && typeof value.schemaVersion === "number"
    && typeof value.vectorScoreKind === "string"
    && typeof value.lexicalAlgorithm === "string"
    && typeof value.tokenUsageKind === "string"
    && typeof value.llmProvider === "string"
    && Array.isArray(value.supportedStrategies)
    && value.supportedStrategies.every((strategy) => typeof strategy === "string")
    && isRecord(value.defaultQuery)
    && typeof value.defaultQuery.topK === "number"
    && typeof value.defaultQuery.mode === "string"
    && typeof value.defaultQuery.reranker === "boolean"
    && typeof value.defaultQuery.minRelevance === "number"
    && typeof value.defaultQuery.neighbors === "boolean"
    && typeof value.defaultQuery.maxContextTokens === "number";
}

function isConfiguredField(value: unknown): value is ConfiguredField {
  if (!isRecord(value)) return false;
  const primitive = value.value === null || typeof value.value === "string" || typeof value.value === "number" || typeof value.value === "boolean";
  return primitive && typeof value.configured === "boolean" && typeof value.id === "string" && typeof value.secret === "boolean";
}

function isConfiguration(value: unknown): value is RuntimeConfiguration {
  return isRecord(value)
    && value.schemaVersion === 1
    && typeof value.sourceOfTruth === "string"
    && Array.isArray(value.groups)
    && value.groups.every((group) => isRecord(group)
      && typeof group.id === "string"
      && typeof group.title === "string"
      && typeof group.description === "string"
      && Array.isArray(group.fields)
      && group.fields.every(isConfiguredField));
}

async function requestEndpoint<T>(path: string, isValid: (value: unknown) => value is T, signal?: AbortSignal): Promise<EndpointResult<T>> {
  try {
    const response = await fetch(`/api/client/${path}`, { cache: "no-store", credentials: "same-origin", signal });
    if (!response.ok) return { status: "error", message: `Endpoint returned HTTP ${response.status}.` };
    const value: unknown = await response.json();
    if (!isValid(value)) return { status: "error", message: "Endpoint response did not match the expected contract." };
    return { status: "connected", data: value };
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    return { status: "error", message: "Could not reach this endpoint through the server-side API proxy." };
  }
}

export async function checkClientStatus(signal?: AbortSignal): Promise<ClientStatus> {
  const [readiness, capabilities, configuration] = await Promise.all([
    requestEndpoint("readiness", isReadiness, signal),
    requestEndpoint("capabilities", isCapabilities, signal),
    requestEndpoint("configuration", isConfiguration, signal),
  ]);
  return { checkedAt: new Date().toISOString(), readiness, capabilities, configuration };
}

export async function testConfiguredProvider(provider: IntegrationStatus["name"], signal?: AbortSignal): Promise<IntegrationStatus> {
  const response = await fetch(`/api/client/checks/${provider}`, {
    method: "POST",
    cache: "no-store",
    credentials: "same-origin",
    headers: { "Content-Type": "application/json" },
    body: "{}",
    signal,
  });
  if (!response.ok) throw new Error(`Provider test returned HTTP ${response.status}.`);
  const value: unknown = await response.json();
  if (!isRecord(value)
    || value.name !== provider
    || (value.status !== "healthy" && value.status !== "failed" && value.status !== "not-configured")
    || typeof value.message !== "string"
    || typeof value.latencyMs !== "number"
    || !isRecord(value.details)) {
    throw new Error("Provider test response did not match the expected contract.");
  }
  return value as unknown as IntegrationStatus;
}
