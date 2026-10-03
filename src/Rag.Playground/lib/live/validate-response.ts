import { isObject, parseRun } from "./parsers";

function record(value: unknown, strings: string[], numbers: string[] = []) {
    if (!isObject(value) || strings.some(key => typeof value[key] !== "string") || numbers.some(key => typeof value[key] !== "number" || !Number.isFinite(value[key])))
        throw new Error("The client API returned an invalid response. Check API/UI version compatibility.");
    return value;
}
function array(value: unknown, validate: (item: unknown) => unknown) {
    if (!Array.isArray(value)) throw new Error("The client API returned an invalid collection.");
    value.forEach(validate);
}
const corpus = (v: unknown) => record(v, ["id", "name", "description"]);
const profile = (v: unknown) => record(v, ["id", "corpusId", "name", "strategy", "embeddingModel", "status"], ["chunkSize", "chunkOverlap", "embeddingDimensions", "documentCount", "chunkCount"]);
const document = (v: unknown) => record(v, ["id", "filename", "content"], ["chunkCount"]);
const job = (v: unknown) => record(v, ["id", "kind", "status"], ["completed", "total"]);

function metrics(value: unknown) {
    const metric = record(value, []);
    for (const key of ["recallAt1", "recallAt5", "mrrAt5", "citationAccuracy", "citationPrecision", "groundedness", "abstentionAccuracy"]) {
        if (metric[key] !== null && (typeof metric[key] !== "number" || !Number.isFinite(metric[key]))) throw new Error("Invalid evaluation metrics.");
    }
}

export function validateResponse(path: string, value: unknown, method = "GET"): void {
    const route = path.split("?")[0];
    if (route === "capabilities") {
        const v = record(value, ["embeddingModel", "documentStore", "vectorStore", "jobStore"], ["embeddingDimensions"]);
        if (typeof v.hybrid !== "boolean" || typeof v.reranker !== "boolean") throw new Error("Invalid client capabilities.");
    } else if (route === "corpora") { if (method === "GET") array(value, corpus); else corpus(value); }
    else if (/^corpora\/[^/]+\/profiles$/.test(route)) { if (method === "GET") array(value, profile); else profile(value); }
    else if (/^profiles\/[^/]+\/documents$/.test(route)) { const v = record(value, [], ["total", "offset", "limit"]); array(v.items, document); }
    else if (/^profiles\/[^/]+\/documents\/[^/]+$/.test(route)) document(value);
    else if (route === "jobs") array(value, job);
    else if (/^jobs\//.test(route) || route.endsWith("/ingestions") || (route === "evaluations" && method === "POST")) job(value);
    else if (/^evaluations\/[^/]+$/.test(route)) {
        const report = record(value, ["id", "status"]);
        if (report.schemaVersion !== undefined && report.schemaVersion !== 1 && report.schemaVersion !== 2) throw new Error("Unsupported evaluation report version.");
        report.schemaVersion ??= 1;
        array(report.profiles, item => {
            const p = record(item, ["profileId", "profileName"], ["embeddingOperations", "averageContextTokens", "averageLatencyMs"]);
            metrics(p.metrics);
            array(p.outcomes, outcome => { const o = record(outcome, ["questionId", "question"]); metrics(o.metrics); parseRun(o.run); });
        });
    }
}
