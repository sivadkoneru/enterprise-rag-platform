import type {
    DemoChunk,
    QueryRequest,
    QueryRun,
    RagGateway,
    RetrievedChunk,
    TraceStage,
} from "@/lib/contracts";
import { chunks, corpora, documents } from "@/lib/demo/corpus";
import { getEvaluationDataset } from "@/lib/evaluation/data";

export function validateRequest(request: QueryRequest): void {
    const c = request.config;
    if (!request.question.trim() || request.question.length > 2000)
        throw new Error("Enter a question between 1 and 2,000 characters.");
    if (!corpora.some((corpus) => corpus.id === request.corpusId))
        throw new Error("Choose an available corpus.");
    if (
        !["fixed", "recursive", "markdown-aware", "semantic"].includes(
            c.strategy,
        ) ||
        !["vector", "hybrid"].includes(c.mode)
    )
        throw new Error("Choose a supported retrieval profile.");
    if (!Number.isInteger(c.topK) || c.topK < 1 || c.topK > 20)
        throw new Error("Top K must be between 1 and 20.");
    if (
        !Number.isFinite(c.minRelevance) ||
        c.minRelevance < 0 ||
        c.minRelevance > 1
    )
        throw new Error("Relevance must be between 0 and 1.");
    if (
        !Number.isInteger(c.chunkSize) ||
        c.chunkSize < 200 ||
        c.chunkSize > 1600 ||
        !Number.isInteger(c.chunkOverlap) ||
        c.chunkOverlap < 0 ||
        c.chunkOverlap >= c.chunkSize
    )
        throw new Error(
            "Chunk size must be 200–1,600 characters; overlap must be smaller than the chunk.",
        );
    if (![384, 768, 1536].includes(c.embeddingDimensions))
        throw new Error("Choose a supported embedding dimension.");
    if (
        !Number.isInteger(c.maxContextTokens) ||
        c.maxContextTokens < 128 ||
        c.maxContextTokens > 8192
    )
        throw new Error("Context budget must be between 128 and 8,192 tokens.");
    if (typeof c.reranker !== "boolean" || typeof c.neighbors !== "boolean")
        throw new Error("Invalid retrieval settings.");
}

function topicFor(question: string): DemoChunk["topic"] | null {
    if (
        /\b(refund|refunds|refundable|return|returns|purchase|purchases|non.?refundable|exceptions|enterprise|contract|reseller|setup fees|custom services)\b/i.test(
            question,
        )
    )
        return "refund";
    if (
        /\b(security|secure|encrypt|encryption|retention|data|access|privacy|sso)\b/i.test(
            question,
        )
    )
        return "security";
    if (
        /\b(api|integrate|integration|authentication|rate limit|endpoint|webhook)\b/i.test(
            question,
        )
    )
        return "api";
    if (
        /\b(support|incident|sla|escalation|ticket|response time)\b/i.test(
            question,
        )
    )
        return "support";
    if (
        /\b(onboarding|onboard|employee|remote|equipment|expense|expenses|new hire|new workspace|connect a source)\b/i.test(
            question,
        )
    )
        return "onboarding";
    if (/\b(evidence|citations|chunking|retrieval|pipeline)\b/i.test(question))
        return "general";
    return null;
}
const clamp = (value: number) =>
    Math.max(0, Math.min(0.99, Number(value.toFixed(3))));
const tokens = (text: string) => Math.ceil(text.length / 4);

/** Deterministic illustrative ranking, not an embedding model or a live search engine. */
export function retrieve(request: QueryRequest): {
    candidates: RetrievedChunk[];
    context: RetrievedChunk[];
    supported: boolean;
} {
    validateRequest(request);
    const config = request.config;
    const corpusDocs = new Set(
        documents
            .filter((doc) => doc.corpusId === request.corpusId)
            .map((doc) => doc.id),
    );
    const source = chunks.filter((chunk) => corpusDocs.has(chunk.documentId));
    const topic = topicFor(request.question);
    const relevant = source.filter((chunk) => chunk.topic === topic);
    const priority = [
        "chunk-128",
        "chunk-244",
        "chunk-19",
        "chunk-301",
        "chunk-99",
    ];
    const focusId = /process|business days|approved refund/i.test(
        request.question,
    )
        ? "chunk-301"
        : /reseller|partner/i.test(request.question)
          ? "chunk-99"
          : /setup fees|custom services|non.?refundable/i.test(request.question)
            ? "chunk-19"
            : /enterprise|contract/i.test(request.question)
              ? "chunk-244"
              : null;
    if (focusId) {
        priority.splice(priority.indexOf(focusId), 1);
        priority.unshift(focusId);
    }
    const queryWords = request.question.toLowerCase().match(/[a-z]{3,}/g) ?? [];
    const conceptOverlap = (chunk: DemoChunk) =>
        queryWords.filter((word) =>
            `${chunk.section} ${chunk.concepts.join(" ")}`
                .toLowerCase()
                .includes(word),
        ).length;
    if (topic === "refund" && request.corpusId === "handbook")
        relevant.sort(
            (a, b) =>
                (priority.includes(a.id) ? priority.indexOf(a.id) : 999) -
                (priority.includes(b.id) ? priority.indexOf(b.id) : 999),
        );
    else relevant.sort((a, b) => conceptOverlap(b) - conceptOverlap(a));
    // Diversify the initial pool so repeated sections from one document do not crowd out other evidence.
    const seenDocuments = new Set<string>();
    const distinct = relevant.filter((chunk) => {
        if (seenDocuments.has(chunk.documentId)) return false;
        seenDocuments.add(chunk.documentId);
        return true;
    });
    const distinctIds = new Set(distinct.map((chunk) => chunk.id));
    const diverse = [
        ...distinct,
        ...relevant.filter((chunk) => !distinctIds.has(chunk.id)),
    ];
    const relevantIds = new Set(relevant.map((chunk) => chunk.id));
    const ordered = [
        ...diverse,
        ...source.filter((chunk) => !relevantIds.has(chunk.id)),
    ];
    const profileShift = {
        fixed: -0.025,
        recursive: 0,
        "markdown-aware": 0.012,
        semantic: -0.07,
    }[config.strategy];
    const sizeShift =
        Math.min(0.018, (config.chunkSize - 800) / 30000) +
        (config.chunkOverlap - 120) / 30000;
    const dimensionShift = (config.embeddingDimensions - 1536) / 100000;
    const ranked = ordered
        .slice(0, Math.max(30, config.topK))
        .map((chunk, i): RetrievedChunk => {
            const matchesTopic = topic !== null && chunk.topic === topic;
            const base = matchesTopic
                ? ([0.89, 0.83, 0.86, 0.73, 0.62][i] ??
                  Math.max(0.35, 0.6 - i * 0.009))
                : 0.28 - i * 0.002;
            const lexicalScore = clamp(
                queryWords.filter((word) =>
                    `${chunk.section} ${chunk.content}`
                        .toLowerCase()
                        .includes(word),
                ).length / Math.max(queryWords.length, 1),
            );
            const vectorScore = clamp(
                base + profileShift + sizeShift + dimensionShift,
            );
            const retrievalScore = clamp(
                config.mode === "hybrid"
                    ? vectorScore * 0.8 + lexicalScore * 0.2 + 0.05
                    : vectorScore,
            );
            const rerankerScore = config.reranker
                ? clamp(
                      matchesTopic
                          ? ([0.94, 0.91, 0.87, 0.78, 0.64][i] ??
                                Math.max(0.33, 0.59 - i * 0.009)) +
                                profileShift +
                                sizeShift +
                                dimensionShift +
                                (config.mode === "hybrid"
                                    ? lexicalScore * 0.025
                                    : 0)
                          : retrievalScore * 0.8,
                  )
                : null;
            const length =
                config.strategy === "semantic"
                    ? Math.min(config.chunkSize, 320)
                    : config.chunkSize + Math.min(config.chunkOverlap, 160);
            const content = chunk.content.slice(0, length);
            return {
                id: chunk.id,
                documentId: chunk.documentId,
                filename: chunk.filename,
                section: chunk.section,
                index: chunk.index,
                content,
                tokens: tokens(content),
                vectorScore,
                lexicalScore,
                retrievalScore,
                rerankerScore,
                score: rerankerScore ?? retrievalScore,
                rankBefore: 0,
                rankAfter: 0,
                inContext: false,
                concepts: chunk.concepts,
            };
        });
    ranked.sort((a, b) => b.retrievalScore - a.retrievalScore);
    ranked.forEach((chunk, index) => {
        chunk.rankBefore = index + 1;
    });
    // Top K is the candidate pool; reranking only reorders those candidates.
    const candidates = ranked
        .slice(0, config.topK)
        .sort((a, b) => b.score - a.score);
    let remaining = config.maxContextTokens;
    for (const [index, chunk] of candidates.entries()) {
        chunk.rankAfter = index + 1;
        if (!topic || !relevantIds.has(chunk.id))
            chunk.exclusionReason = "No supported topic match";
        else if (chunk.score < config.minRelevance)
            chunk.exclusionReason = "Below relevance threshold";
        else if (chunk.tokens > remaining)
            chunk.exclusionReason = "Exceeds remaining context budget";
        else {
            chunk.inContext = true;
            remaining -= chunk.tokens;
        }
    }
    const context = candidates.filter((chunk) => chunk.inContext);
    if (config.neighbors) {
        for (const parent of [...context]) {
            const docChunks = source.filter(
                (chunk) => chunk.documentId === parent.documentId,
            );
            const next =
                docChunks[
                    docChunks.findIndex((chunk) => chunk.id === parent.id) + 1
                ];
            if (
                !next ||
                candidates.some((chunk) => chunk.id === next.id) ||
                context.some((chunk) => chunk.id === next.id)
            )
                continue;
            const content = next.content.slice(0, config.chunkSize);
            if (tokens(content) > remaining) continue;
            context.push({
                ...parent,
                id: next.id,
                index: next.index,
                section: next.section,
                content,
                tokens: tokens(content),
                concepts: next.concepts,
                isNeighbor: true,
                rankBefore: 0,
                rankAfter: 0,
                inContext: true,
            });
            remaining -= tokens(content);
        }
    }
    return {
        candidates,
        context,
        supported: context.some((chunk) => !chunk.isNeighbor),
    };
}

export function createRun(request: QueryRequest): QueryRun {
    const { candidates, context, supported } = retrieve(request);
    const primary = context.filter((chunk) => !chunk.isNeighbor);
    // A prepared answer sentence is emitted only when its full evidence is in context.
    const seenStatements = new Set<string>();
    const evidence = primary
        .map((chunk) => ({
            chunk,
            source: chunks.find((source) => source.id === chunk.id),
        }))
        .filter((item) => {
            if (
                !item.source ||
                !item.chunk.content.includes(item.source.summary) ||
                seenStatements.has(item.source.summary)
            )
                return false;
            seenStatements.add(item.source.summary);
            return true;
        })
        .slice(0, 3);
    const abstained = !supported || evidence.length === 0;
    const citations = abstained
        ? []
        : evidence.map(({ chunk }, i) => ({
              number: i + 1,
              chunkId: chunk.id,
              documentId: chunk.documentId,
          }));
    const answer = abstained
        ? [
              {
                  text: "There is not enough supporting evidence in the selected context to answer this question. Try another corpus, a lower relevance threshold, or a larger context budget.",
              },
          ]
        : evidence.map(({ source }, i) => ({
              text: source!.summary,
              citationNumber: i + 1,
          }));
    const contextTokens = context.reduce((sum, chunk) => sum + chunk.tokens, 0);
    const outputTokens = tokens(
        answer.map((segment) => segment.text).join(" "),
    );
    const trace: TraceStage[] = [
        {
            id: "query",
            name: "Query",
            status: "queued",
            durationMs: 3,
            detail: "Input validated",
            diagnostics: {
                characters: request.question.length,
                corpus: request.corpusId,
            },
        },
        {
            id: "enhancement",
            name: "Query enhancement",
            status: "queued",
            durationMs: 21,
            detail: "Intent + normalization",
            diagnostics: {
                intent: topicFor(request.question) ?? "unsupported",
                normalized: request.question.trim().toLowerCase(),
                method: "Local deterministic topic matching",
            },
        },
        {
            id: "embedding",
            name: "Embedding",
            status: "queued",
            durationMs: Math.round(
                (32 * request.config.embeddingDimensions) / 1536,
            ),
            detail: `${request.config.embeddingDimensions.toLocaleString()} dimensions`,
            diagnostics: {
                dimensions: request.config.embeddingDimensions,
                provider: "Simulated embedding profile",
                networkCalls: 0,
            },
        },
        {
            id: "search",
            name:
                request.config.mode === "hybrid"
                    ? "Hybrid search"
                    : "Vector search",
            status: "queued",
            durationMs:
                13 +
                request.config.topK +
                (request.config.mode === "hybrid" ? 6 : 0),
            detail: `${candidates.length} candidates`,
            diagnostics: {
                strategy: request.config.strategy,
                topK: request.config.topK,
                mode: request.config.mode,
                score: "Illustrative local ranking; not cosine probability",
            },
        },
        {
            id: "reranking",
            name: "Reranking",
            status: "queued",
            durationMs: request.config.reranker ? 9 : 0,
            detail: request.config.reranker ? "Evidence relevance" : "Disabled",
            diagnostics: {
                enabled: request.config.reranker,
                candidateCount: candidates.length,
            },
        },
        {
            id: "context",
            name: "Context assembly",
            status: "queued",
            durationMs: 7,
            detail: `${context.length} chunks · ${contextTokens} tokens`,
            diagnostics: {
                admitted: context.length,
                estimatedTokens: contextTokens,
                budget: request.config.maxContextTokens,
                neighboringChunks: context.filter((chunk) => chunk.isNeighbor)
                    .length,
            },
        },
        {
            id: "generation",
            name: "Generation",
            status: "queued",
            durationMs: abstained ? 45 : 411,
            detail: abstained
                ? "Insufficient evidence"
                : "Grounded fixture response",
            diagnostics: {
                provider: "Deterministic extractive demo",
                estimatedOutputTokens: outputTokens,
                externalRequests: 0,
            },
        },
        {
            id: "validation",
            name: "Citation validation",
            status: "queued",
            durationMs: 17,
            detail: `${citations.length} references checked`,
            diagnostics: {
                resolved: citations.length,
                invalid: 0,
                validation:
                    "Citation IDs and answer sentences resolve to admitted evidence",
            },
        },
    ];
    return {
        id: crypto.randomUUID(),
        createdAt: new Date().toISOString(),
        request: structuredClone(request),
        answer,
        citations,
        candidates,
        context,
        trace,
        totalLatencyMs: trace.reduce((sum, stage) => sum + stage.durationMs, 0),
        contextTokens,
        outputTokens,
        confidence: abstained
            ? "Insufficient"
            : evidence.length >= 3
              ? "High"
              : "Partial",
        confidenceReason: abstained
            ? "No complete supporting sentence was admitted to context."
            : `${evidence.length} cited statements have complete supporting sentences in context. This is evidence coverage, not a probability of correctness.`,
        abstained,
        provenance: "demo-fixture",
    };
}

function delay(ms: number, signal: AbortSignal): Promise<void> {
    return new Promise((resolve, reject) => {
        if (signal.aborted) {
            reject(new DOMException("Canceled", "AbortError"));
            return;
        }
        const abort = () => {
            clearTimeout(timer);
            reject(new DOMException("Canceled", "AbortError"));
        };
        const timer = setTimeout(() => {
            signal.removeEventListener("abort", abort);
            resolve();
        }, ms);
        signal.addEventListener("abort", abort, { once: true });
    });
}

export class DemoRagGateway implements RagGateway {
    async listCorpora() {
        return corpora;
    }
    async listDocuments(corpusId: QueryRequest["corpusId"]) {
        return documents.filter((doc) => doc.corpusId === corpusId);
    }
    async getDocument(documentId: string) {
        const doc = documents.find((item) => item.id === documentId);
        if (!doc) throw new Error("Document not found.");
        return doc;
    }
    async getEvaluation() {
        return getEvaluationDataset();
    }
    async runQuery(
        request: QueryRequest,
        { signal, onEvent }: Parameters<RagGateway["runQuery"]>[1],
    ) {
        const run = createRun(request);
        const emit = () => onEvent({ stages: structuredClone(run.trace) });
        emit();
        for (const stage of run.trace) {
            if (signal.aborted)
                throw new DOMException("Canceled", "AbortError");
            if (stage.id === "reranking" && !request.config.reranker) {
                stage.status = "skipped";
                emit();
                continue;
            }
            stage.status = "running";
            emit();
            await delay(stage.id === "generation" ? 430 : 110, signal);
            if (request.simulateFailure && stage.id === "embedding") {
                stage.status = "failed";
                stage.detail = "Simulated provider timeout";
                emit();
                throw new Error(
                    "Simulated embedding timeout. Retry to run with the healthy demo provider.",
                );
            }
            stage.status = "complete";
            emit();
        }
        return run;
    }
}
export const gateway = new DemoRagGateway();
