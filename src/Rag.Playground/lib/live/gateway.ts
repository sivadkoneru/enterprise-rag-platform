import type { TraceStage } from "@/lib/contracts";
import type { ClientCorpus, ClientDocument, ClientJob, EnvironmentCapabilities, EvaluationDatasetInput, EvaluationRun, IndexProfile, LiveQueryRequest, LiveQueryRun, PageResult } from "./contracts";
import { clientRequest } from "./http";

function isObject(value: unknown): value is Record<string, unknown> {
    return typeof value === "object" && value !== null && !Array.isArray(value);
}
export function parseStage(value: unknown): TraceStage {
    if (!isObject(value) || typeof value.id !== "string" || typeof value.name !== "string" || typeof value.durationMs !== "number" || !Number.isFinite(value.durationMs) || typeof value.status !== "string" || !["queued", "running", "complete", "skipped", "failed", "canceled"].includes(value.status) || typeof value.detail !== "string" || !isObject(value.diagnostics))
        throw new Error("The API returned an invalid pipeline stage.");
    return value as unknown as TraceStage;
}
export function parseRun(value: unknown): LiveQueryRun {
    if (!isObject(value) || typeof value.id !== "string" || typeof value.answer !== "string" || typeof value.question !== "string" || typeof value.profileId !== "string" || typeof value.corpusId !== "string" || typeof value.abstained !== "boolean")
        throw new Error("The API returned an invalid query result.");
    for (const key of ["candidates", "context", "trace", "citations", "invalidCitations"])
        if (!Array.isArray(value[key])) throw new Error(`The API query result is missing ${key}.`);
    for (const key of ["totalLatencyMs", "contextTokens", "outputTokens"])
        if (typeof value[key] !== "number" || !Number.isFinite(value[key])) throw new Error("The API returned invalid query metrics.");
    for (const chunk of [...value.candidates as unknown[], ...value.context as unknown[]]) {
        if (!isObject(chunk) || typeof chunk.id !== "string" || typeof chunk.documentId !== "string" || typeof chunk.content !== "string" || typeof chunk.filename !== "string" || (chunk.vectorScore !== null && (typeof chunk.vectorScore !== "number" || !Number.isFinite(chunk.vectorScore))))
            throw new Error("The API returned an invalid retrieved chunk.");
    }
    for (const citation of value.citations as unknown[]) {
        if (!isObject(citation) || typeof citation.number !== "number" || typeof citation.chunkId !== "string" || typeof citation.valid !== "boolean")
            throw new Error("The API returned an invalid citation.");
    }
    (value.trace as unknown[]).forEach(parseStage);
    return value as unknown as LiveQueryRun;
}

/** A live gateway uses API data exclusively; it never falls back to demo fixtures. */
export class LiveRagGateway {
    capabilities(signal?: AbortSignal) { return clientRequest<EnvironmentCapabilities>("capabilities", { signal }); }
    listCorpora(signal?: AbortSignal) { return clientRequest<ClientCorpus[]>("corpora", { signal }); }
    listProfiles(corpusId: string, signal?: AbortSignal) { return clientRequest<IndexProfile[]>(`corpora/${encodeURIComponent(corpusId)}/profiles`, { signal }); }
    listDocuments(profileId: string, offset = 0, signal?: AbortSignal) { return clientRequest<PageResult<ClientDocument>>(`profiles/${encodeURIComponent(profileId)}/documents?offset=${offset}&limit=20`, { signal }); }
    getDocument(profileId: string, documentId: string, signal?: AbortSignal) { return clientRequest<ClientDocument>(`profiles/${encodeURIComponent(profileId)}/documents/${encodeURIComponent(documentId)}`, { signal }); }
    createCorpus(name: string, description: string) { return clientRequest<ClientCorpus>("corpora", { method: "POST", body: JSON.stringify({ name, description }) }); }
    createProfile(corpusId: string, profile: Omit<IndexProfile, "id" | "corpusId" | "status" | "documentCount" | "chunkCount" | "createdAt">) { return clientRequest<IndexProfile>(`corpora/${encodeURIComponent(corpusId)}/profiles`, { method: "POST", body: JSON.stringify(profile) }); }
    ingest(profileId: string, sources: string[]) { return clientRequest<ClientJob>(`profiles/${encodeURIComponent(profileId)}/ingestions`, { method: "POST", body: JSON.stringify({ sources }) }); }
    getJob(id: string, signal?: AbortSignal) { return clientRequest<ClientJob>(`jobs/${encodeURIComponent(id)}`, { signal }); }
    listJobs(signal?: AbortSignal) { return clientRequest<ClientJob[]>("jobs", { signal }); }
    controlJob(id: string, action: "pause" | "resume" | "cancel") { return clientRequest<ClientJob>(`jobs/${encodeURIComponent(id)}/${action}`, { method: "POST", body: "{}" }); }
    evaluate(input: EvaluationDatasetInput) { return clientRequest<ClientJob>("evaluations", { method: "POST", body: JSON.stringify(input) }); }
    getEvaluation(id: string, signal?: AbortSignal) { return clientRequest<EvaluationRun>(`evaluations/${encodeURIComponent(id)}`, { signal }); }

    async runQuery(request: LiveQueryRequest, options: { signal: AbortSignal; onStage: (stage: TraceStage) => void }): Promise<LiveQueryRun> {
        const response = await fetch("/api/client/queries", { method: "POST", headers: { "Content-Type": "application/json", Accept: "text/event-stream" }, body: JSON.stringify(request), signal: options.signal, cache: "no-store" });
        if (!response.ok || !response.body) {
            const error: unknown = await response.json().catch(() => null);
            throw new Error(isObject(error) && typeof error.detail === "string" ? error.detail : `Query failed (${response.status}). Check your client services.`);
        }
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";
        let result: LiveQueryRun | null = null;
        try {
            while (true) {
                const { value, done } = await reader.read();
                buffer = (buffer + decoder.decode(value, { stream: !done })).replace(/\r\n/g, "\n");
                let boundary: number;
                while ((boundary = buffer.indexOf("\n\n")) !== -1) {
                    const block = buffer.slice(0, boundary);
                    buffer = buffer.slice(boundary + 2);
                    const lines = block.split("\n");
                    const event = lines.find(line => line.startsWith("event:"))?.slice(6).trim();
                    const data = lines.filter(line => line.startsWith("data:")).map(line => line.slice(5).trimStart()).join("\n");
                    if (!data) continue;
                    const parsed: unknown = JSON.parse(data);
                    if (event === "stage") options.onStage(parseStage(parsed));
                    if (event === "result") result = parseRun(parsed);
                    if (event === "error") throw new Error(isObject(parsed) && typeof parsed.message === "string" ? parsed.message : "Query execution failed.");
                }
                if (done) break;
            }
        } finally { await reader.cancel().catch(() => {}); reader.releaseLock(); }
        if (!result) throw new Error("The query stream ended before a result arrived. Retry the query.");
        return result;
    }
}
export const liveGateway = new LiveRagGateway();
