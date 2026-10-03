import type { TraceStage } from "@/lib/contracts";
import type { LiveQueryRun } from "./contracts";
export function isObject(value: unknown): value is Record<string, unknown> {
    return typeof value === "object" && value !== null && !Array.isArray(value);
}
export function parseStage(value: unknown): TraceStage {
    if (
        !isObject(value) ||
        typeof value.id !== "string" ||
        typeof value.name !== "string" ||
        typeof value.durationMs !== "number" ||
        !Number.isFinite(value.durationMs) ||
        typeof value.status !== "string" ||
        ![
            "queued",
            "running",
            "complete",
            "skipped",
            "failed",
            "canceled",
        ].includes(value.status) ||
        typeof value.detail !== "string" ||
        !isObject(value.diagnostics)
    )
        throw new Error("The API returned an invalid pipeline stage.");
    return value as unknown as TraceStage;
}
export function parseRun(value: unknown): LiveQueryRun {
    if (
        !isObject(value) ||
        typeof value.id !== "string" ||
        typeof value.answer !== "string" ||
        typeof value.question !== "string" ||
        typeof value.profileId !== "string" ||
        typeof value.corpusId !== "string" ||
        typeof value.abstained !== "boolean"
    )
        throw new Error("The API returned an invalid query result.");
    for (const key of [
        "candidates",
        "context",
        "trace",
        "citations",
        "invalidCitations",
    ])
        if (!Array.isArray(value[key]))
            throw new Error(`The API query result is missing ${key}.`);
    for (const key of ["totalLatencyMs", "contextTokens", "outputTokens"])
        if (typeof value[key] !== "number" || !Number.isFinite(value[key]))
            throw new Error("The API returned invalid query metrics.");
    for (const key of [
        "provider",
        "embeddingModel",
        "chatModel",
        "systemPromptHash",
        "traceId",
    ])
        if (value[key] != null && typeof value[key] !== "string")
            throw new Error("The API returned invalid run provenance.");
    for (const key of [
        "embeddingTokens",
        "embeddingCalls",
        "promptTokens",
        "totalTokens",
    ])
        if (
            value[key] != null &&
            (typeof value[key] !== "number" ||
                !Number.isSafeInteger(value[key]) ||
                value[key] < 0)
        )
            throw new Error("The API returned invalid usage metrics.");
    if (
        typeof value.createdAt !== "string" ||
        typeof value.tokenUsageKind !== "string" ||
        !(value.invalidCitations as unknown[]).every(
            (item) => typeof item === "string",
        )
    )
        throw new Error("The API returned invalid run metadata.");
    for (const chunk of [
        ...(value.candidates as unknown[]),
        ...(value.context as unknown[]),
    ]) {
        if (
            !isObject(chunk) ||
            typeof chunk.id !== "string" ||
            typeof chunk.documentId !== "string" ||
            typeof chunk.content !== "string" ||
            typeof chunk.filename !== "string" ||
            (chunk.vectorScore !== null &&
                (typeof chunk.vectorScore !== "number" ||
                    !Number.isFinite(chunk.vectorScore)))
        )
            throw new Error("The API returned an invalid retrieved chunk.");
        for (const key of ["lexicalScore", "fusionScore", "rerankerScore"])
            if (
                chunk[key] !== null &&
                (typeof chunk[key] !== "number" || !Number.isFinite(chunk[key]))
            )
                throw new Error("The API returned an invalid chunk score.");
        for (const key of ["index", "rankBefore", "rankAfter"])
            if (
                typeof chunk[key] !== "number" ||
                !Number.isSafeInteger(chunk[key])
            )
                throw new Error("The API returned an invalid chunk rank.");
        if (typeof chunk.inContext !== "boolean")
            throw new Error("The API returned invalid context admission.");
    }
    for (const citation of value.citations as unknown[]) {
        if (
            !isObject(citation) ||
            typeof citation.number !== "number" ||
            !Number.isSafeInteger(citation.number) ||
            typeof citation.chunkId !== "string" ||
            typeof citation.documentId !== "string" ||
            typeof citation.valid !== "boolean"
        )
            throw new Error("The API returned an invalid citation.");
    }
    (value.trace as unknown[]).forEach(parseStage);
    return value as unknown as LiveQueryRun;
}
