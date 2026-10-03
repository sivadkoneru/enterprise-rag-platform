import type { TraceStage } from "@/lib/contracts";
import type { ClientCorpus, ClientDocument, ClientJob, EnvironmentCapabilities, EvaluationDatasetInput, EvaluationRun, IndexProfile, LiveQueryRequest, LiveQueryRun, PageResult } from "./contracts";
import { clientRequest } from "./http";

import { isObject, parseRun, parseStage } from "./parsers";
export { parseRun, parseStage } from "./parsers";
import { readEvents } from "./sse";

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
        let result: LiveQueryRun | null = null;
        for await (const { event, data } of readEvents(response.body, options.signal)) {
            const parsed: unknown = JSON.parse(data);
            if (event === "stage") options.onStage(parseStage(parsed));
            if (event === "result") result = parseRun(parsed);
            if (event === "error") throw new Error(isObject(parsed) && typeof parsed.message === "string" ? parsed.message : "Query execution failed.");
        }
        if (!result) throw new Error("The query stream ended before a result arrived. Retry the query.");
        return result;
    }
}
export const liveGateway = new LiveRagGateway();
