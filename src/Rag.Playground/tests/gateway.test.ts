import { afterEach, describe, expect, it, vi } from "vitest";
import type {
    PipelineEvent,
    QueryRequest,
    RetrievalConfig,
} from "@/lib/contracts";
import { DEFAULT_CONFIG, DEFAULT_QUESTION } from "@/lib/constants";
import { chunks, corpora, documents } from "@/lib/demo/corpus";
import {
    createRun,
    DemoRagGateway,
    retrieve,
    validateRequest,
} from "@/lib/demo/gateway";

const request = (
    config: Partial<RetrievalConfig> = {},
    question = DEFAULT_QUESTION,
): QueryRequest => ({
    corpusId: "handbook",
    question,
    config: { ...DEFAULT_CONFIG, ...config },
});
afterEach(() => vi.useRealTimers());

describe("demo corpus integrity", () => {
    it("reports real document and chunk counts for each corpus", () => {
        expect(
            corpora.map((corpus) => [
                corpus.id,
                corpus.documentCount,
                corpus.chunkCount,
            ]),
        ).toEqual([
            ["handbook", 42, 1284],
            ["product", 12, 240],
            ["support", 18, 360],
        ]);
        for (const corpus of corpora) {
            const ownDocuments = documents.filter(
                (document) => document.corpusId === corpus.id,
            );
            expect(ownDocuments).toHaveLength(corpus.documentCount);
            expect(
                ownDocuments.reduce(
                    (count, document) => count + document.chunkCount,
                    0,
                ),
            ).toBe(corpus.chunkCount);
        }
    });

    it("resolves every unique chunk ID to its document and original text", () => {
        expect(new Set(chunks.map((chunk) => chunk.id)).size).toBe(
            chunks.length,
        );
        expect(new Set(documents.map((document) => document.id)).size).toBe(
            documents.length,
        );
        for (const chunk of chunks) {
            const document = documents.find(
                (document) => document.id === chunk.documentId,
            );
            expect(document, chunk.id).toBeDefined();
            expect(document!.content, chunk.id).toContain(chunk.content);
            expect(document!.filename, chunk.id).toBe(chunk.filename);
        }
    });

    it("keeps each prepared refund evidence sentence literally inside its source", () => {
        for (const id of [
            "chunk-128",
            "chunk-244",
            "chunk-19",
            "chunk-301",
            "chunk-99",
        ]) {
            const chunk = chunks.find((chunk) => chunk.id === id);
            expect(chunk, id).toBeDefined();
            expect(chunk!.content, id).toContain(chunk!.summary);
        }
    });
});

describe("default grounded query", () => {
    it("returns five seeded candidates, four context chunks, and three distinct cited statements", () => {
        const run = createRun(request());
        expect(run.candidates.map((chunk) => chunk.id)).toEqual([
            "chunk-128",
            "chunk-244",
            "chunk-19",
            "chunk-301",
            "chunk-99",
        ]);
        expect(run.context).toHaveLength(4);
        expect(run.citations).toHaveLength(3);
        expect(new Set(run.answer.map((segment) => segment.text)).size).toBe(3);
        expect(run.abstained).toBe(false);
        expect(run.provenance).toBe("demo-fixture");
        expect(run.confidence).toBe("High");
    });

    it("grounds each exact answer sentence in an admitted chunk with a resolvable citation", () => {
        const run = createRun(request());
        for (const segment of run.answer) {
            const citation = run.citations.find(
                (citation) => citation.number === segment.citationNumber,
            );
            expect(citation).toBeDefined();
            const context = run.context.find(
                (chunk) => chunk.id === citation!.chunkId,
            );
            expect(context).toBeDefined();
            expect(context!.documentId).toBe(citation!.documentId);
            expect(context!.content).toContain(segment.text);
        }
        expect(run.contextTokens).toBe(
            run.context.reduce((sum, chunk) => sum + chunk.tokens, 0),
        );
        expect(run.trace.every((stage) => stage.status === "queued")).toBe(
            true,
        );
    });

    it("reorders the same top-K pool with reranking and records both ranks", () => {
        const raw = retrieve(request({ reranker: false }));
        const reranked = retrieve(request());
        expect(reranked.candidates.map((chunk) => chunk.id).sort()).toEqual(
            raw.candidates.map((chunk) => chunk.id).sort(),
        );
        expect(reranked.candidates.map((chunk) => chunk.id)).not.toEqual(
            raw.candidates.map((chunk) => chunk.id),
        );
        expect(
            reranked.candidates.some(
                (chunk) => chunk.rankBefore !== chunk.rankAfter,
            ),
        ).toBe(true);
        expect(
            raw.candidates.every(
                (chunk) =>
                    chunk.rerankerScore === null &&
                    chunk.rankBefore === chunk.rankAfter,
            ),
        ).toBe(true);
    });
});

describe("retrieval controls", () => {
    it.each([1, 3, 10, 20])(
        "uses top K %i as the candidate pool bound",
        (topK) => {
            const run = createRun(request({ topK }));
            expect(run.candidates).toHaveLength(topK);
            expect(run.context.length).toBeLessThanOrEqual(topK);
            expect(run.candidates.map((chunk) => chunk.rankAfter)).toEqual(
                Array.from({ length: topK }, (_, index) => index + 1),
            );
        },
    );

    it("excludes evidence above its relevance score and abstains when none is admitted", () => {
        const high = createRun(request({ minRelevance: 0.99 }));
        expect(high.context).toHaveLength(0);
        expect(
            high.candidates.every(
                (chunk) =>
                    chunk.exclusionReason === "Below relevance threshold",
            ),
        ).toBe(true);
        expect(high.abstained).toBe(true);
        expect(high.citations).toHaveLength(0);
        const relaxed = retrieve(request({ minRelevance: 0 }));
        expect(relaxed.context).toHaveLength(5);
    });

    it("respects small context budgets with explicit exclusion reasons", () => {
        const limited = createRun(request({ maxContextTokens: 128 }));
        expect(limited.contextTokens).toBeLessThanOrEqual(128);
        expect(limited.context.length).toBeLessThan(
            createRun(request()).context.length,
        );
        expect(
            limited.candidates.some(
                (chunk) =>
                    chunk.exclusionReason ===
                    "Exceeds remaining context budget",
            ),
        ).toBe(true);
        expect(
            limited.context.every(
                (chunk) => chunk.tokens === Math.ceil(chunk.content.length / 4),
            ),
        ).toBe(true);
    });

    it("adds unique adjacent context only when enabled and within budget", () => {
        const base = retrieve(request());
        const neighbors = retrieve(request({ neighbors: true }));
        expect(neighbors.candidates.map((chunk) => chunk.id)).toEqual(
            base.candidates.map((chunk) => chunk.id),
        );
        expect(neighbors.context.some((chunk) => chunk.isNeighbor)).toBe(true);
        expect(new Set(neighbors.context.map((chunk) => chunk.id)).size).toBe(
            neighbors.context.length,
        );
        expect(
            neighbors.context.reduce((sum, chunk) => sum + chunk.tokens, 0),
        ).toBeLessThanOrEqual(DEFAULT_CONFIG.maxContextTokens);
        for (const neighbor of neighbors.context.filter(
            (chunk) => chunk.isNeighbor,
        )) {
            const source = chunks.find((chunk) => chunk.id === neighbor.id);
            expect(source).toBeDefined();
            expect(neighbor.content).toBe(
                source!.content.slice(0, DEFAULT_CONFIG.chunkSize),
            );
            expect(neighbor.documentId).toBe(source!.documentId);
        }
    });

    it("changes scores, context length, and diagnostic profiles when controls change", () => {
        const base = createRun(request());
        const hybrid = createRun(request({ mode: "hybrid" }));
        expect(
            hybrid.candidates.map((chunk) => chunk.retrievalScore),
        ).not.toEqual(base.candidates.map((chunk) => chunk.retrievalScore));
        expect(hybrid.trace.find((stage) => stage.id === "search")?.name).toBe(
            "Hybrid search",
        );
        const semantic = createRun(request({ strategy: "semantic" }));
        expect(semantic.contextTokens).toBeLessThan(base.contextTokens);
        expect(
            semantic.candidates.every((chunk) => chunk.content.length <= 320),
        ).toBe(true);
        const small = createRun(
            request({
                chunkSize: 200,
                chunkOverlap: 0,
                embeddingDimensions: 384,
            }),
        );
        expect(
            small.candidates.every((chunk) => chunk.content.length <= 200),
        ).toBe(true);
        expect(small.candidates[0].vectorScore).not.toBe(
            base.candidates[0].vectorScore,
        );
        expect(
            small.trace.find((stage) => stage.id === "embedding")?.diagnostics
                .dimensions,
        ).toBe(384);
        const markdown = createRun(request({ strategy: "markdown-aware" }));
        expect(markdown.candidates[0].vectorScore).not.toBe(
            base.candidates[0].vectorScore,
        );
    });

    it("rejects unsupported topics despite lowering the threshold", () => {
        const run = createRun(
            request(
                { minRelevance: 0 },
                "What is the orbital period of Jupiter?",
            ),
        );
        expect(run.context).toHaveLength(0);
        expect(run.citations).toHaveLength(0);
        expect(run.abstained).toBe(true);
        expect(run.confidence).toBe("Insufficient");
        expect(
            run.candidates.every(
                (chunk) => chunk.exclusionReason === "No supported topic match",
            ),
        ).toBe(true);
    });

    it("scopes all candidates and context to the selected corpus", () => {
        const input = request({}, "What security controls protect access?");
        input.corpusId = "product";
        const run = createRun(input);
        const validIds = new Set(
            documents
                .filter((document) => document.corpusId === "product")
                .map((document) => document.id),
        );
        expect(run.candidates.length).toBeGreaterThan(0);
        expect(
            [...run.candidates, ...run.context].every((chunk) =>
                validIds.has(chunk.documentId),
            ),
        ).toBe(true);
    });
});

describe("untrusted request validation", () => {
    it.each<Partial<RetrievalConfig>>([
        { topK: 0 },
        { topK: 21 },
        { topK: 1.5 },
        { minRelevance: NaN },
        { minRelevance: Infinity },
        { minRelevance: -0.1 },
        { minRelevance: 1.1 },
        { chunkSize: 199 },
        { chunkSize: 1601 },
        { chunkOverlap: -1 },
        { chunkOverlap: 800 },
        { embeddingDimensions: 100 },
        { maxContextTokens: 127 },
        { maxContextTokens: 8193 },
    ])("rejects invalid numerical controls %j", (config) => {
        expect(() => validateRequest(request(config))).toThrow();
        expect(() => createRun(request(config))).toThrow();
    });
    it.each(["", "  ", "x".repeat(2001)])(
        "rejects missing or oversized question",
        (question) => {
            expect(() => validateRequest(request({}, question))).toThrow(
                /question/i,
            );
        },
    );
    it("rejects unsupported runtime selector and boolean values", () => {
        for (const input of [
            { ...request(), corpusId: "missing" },
            {
                ...request(),
                config: { ...DEFAULT_CONFIG, strategy: "missing" },
            },
            { ...request(), config: { ...DEFAULT_CONFIG, mode: "missing" } },
            { ...request(), config: { ...DEFAULT_CONFIG, reranker: "true" } },
            { ...request(), config: { ...DEFAULT_CONFIG, neighbors: 1 } },
        ]) {
            // Deliberately bypass static types to exercise values arriving from an external caller.
            expect(() => validateRequest(input as QueryRequest)).toThrow();
        }
    });
});

describe("pipeline transitions", () => {
    const gateway = new DemoRagGateway();
    it("emits immutable queued/running/complete snapshots in stage order", async () => {
        vi.useFakeTimers();
        const events: PipelineEvent[] = [];
        const pending = gateway.runQuery(request(), {
            signal: new AbortController().signal,
            onEvent: (event) => events.push(event),
        });
        await vi.runAllTimersAsync();
        const run = await pending;
        expect(
            events[0].stages.every((stage) => stage.status === "queued"),
        ).toBe(true);
        expect(
            events.at(-1)?.stages.every((stage) => stage.status === "complete"),
        ).toBe(true);
        expect(run.trace.every((stage) => stage.status === "complete")).toBe(
            true,
        );
        expect(
            events.flatMap((event) =>
                event.stages
                    .filter((stage) => stage.status === "running")
                    .map((stage) => stage.id),
            ),
        ).toEqual(run.trace.map((stage) => stage.id));
        expect(events[0].stages[0].status).toBe("queued");
    });

    it("skips a disabled reranker without a running transition", async () => {
        vi.useFakeTimers();
        const events: PipelineEvent[] = [];
        const pending = gateway.runQuery(request({ reranker: false }), {
            signal: new AbortController().signal,
            onEvent: (event) => events.push(event),
        });
        await vi.runAllTimersAsync();
        const run = await pending;
        expect(
            run.trace.find((stage) => stage.id === "reranking")?.status,
        ).toBe("skipped");
        expect(
            events.some(
                (event) =>
                    event.stages.find((stage) => stage.id === "reranking")
                        ?.status === "running",
            ),
        ).toBe(false);
    });

    it("aborts before work and during a running stage", async () => {
        const controller = new AbortController();
        controller.abort();
        await expect(
            gateway.runQuery(request(), {
                signal: controller.signal,
                onEvent: () => undefined,
            }),
        ).rejects.toMatchObject({ name: "AbortError" });
        vi.useFakeTimers();
        const during = new AbortController();
        const events: PipelineEvent[] = [];
        const pending = gateway.runQuery(request(), {
            signal: during.signal,
            onEvent: (event) => {
                events.push(event);
                if (
                    event.stages.find((stage) => stage.id === "embedding")
                        ?.status === "running"
                )
                    during.abort();
            },
        });
        const rejection = expect(pending).rejects.toMatchObject({
            name: "AbortError",
        });
        await vi.runAllTimersAsync();
        await rejection;
        expect(
            events.at(-1)?.stages.find((stage) => stage.id === "search")
                ?.status,
        ).toBe("queued");
        expect(
            events.some((event) =>
                event.stages.some((stage) => stage.status === "failed"),
            ),
        ).toBe(false);
    });

    it("fails embedding explicitly and completes a fresh healthy retry", async () => {
        vi.useFakeTimers();
        const failedEvents: PipelineEvent[] = [];
        const pending = gateway.runQuery(
            { ...request(), simulateFailure: true },
            {
                signal: new AbortController().signal,
                onEvent: (event) => failedEvents.push(event),
            },
        );
        const failure = expect(pending).rejects.toThrow(/embedding timeout/i);
        await vi.runAllTimersAsync();
        await failure;
        expect(
            failedEvents
                .at(-1)
                ?.stages.find((stage) => stage.id === "embedding")?.status,
        ).toBe("failed");
        expect(
            failedEvents.at(-1)?.stages.find((stage) => stage.id === "search")
                ?.status,
        ).toBe("queued");
        const retryEvents: PipelineEvent[] = [];
        const retry = gateway.runQuery(request(), {
            signal: new AbortController().signal,
            onEvent: (event) => retryEvents.push(event),
        });
        await vi.runAllTimersAsync();
        expect(
            (await retry).trace.every((stage) => stage.status === "complete"),
        ).toBe(true);
        expect(
            retryEvents[0].stages.every((stage) => stage.status === "queued"),
        ).toBe(true);
        expect(
            failedEvents
                .at(-1)
                ?.stages.find((stage) => stage.id === "embedding")?.status,
        ).toBe("failed");
    });
});
