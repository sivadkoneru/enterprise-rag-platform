import { afterEach, describe, expect, it, vi } from "vitest";
import { LiveRagGateway, parseRun } from "@/lib/live/gateway";
import { parseEvaluationQuestions } from "@/lib/live/evaluation";
import type { LiveQueryRequest, LiveQueryRun } from "@/lib/live/contracts";

const request: LiveQueryRequest = { question: "What is the policy?", corpusId: "corpus", profileId: "profile", topK: 5, mode: "vector", reranker: false, minRelevance: 0.7, neighbors: false, maxContextTokens: 4096 };
const run: LiveQueryRun = { id: "run", createdAt: "2026-01-01T00:00:00Z", question: request.question, corpusId: "corpus", profileId: "profile", answer: "Insufficient evidence.", citations: [], candidates: [], context: [], trace: [], totalLatencyMs: 12, contextTokens: 0, outputTokens: 6, tokenUsageKind: "estimated-chars/4", abstained: true, invalidCitations: [] };
afterEach(() => vi.unstubAllGlobals());

describe("live gateway", () => {
    it("reads split SSE frames and never invents a demo response", async () => {
        const stage = { id: "embedding", name: "Embedding", status: "complete", durationMs: 12, detail: "Measured", diagnostics: {} };
        const text = `event: stage\r\ndata: ${JSON.stringify(stage)}\r\n\r\nevent: result\r\ndata: ${JSON.stringify(run)}\r\n\r\n`;
        const bytes = new TextEncoder().encode(text);
        const stream = new ReadableStream({ start(controller) { for (let i = 0; i < bytes.length; i += 7) controller.enqueue(bytes.slice(i, i + 7)); controller.close(); } });
        const fetchMock = vi.fn().mockResolvedValue(new Response(stream, { headers: { "content-type": "text/event-stream" } }));
        vi.stubGlobal("fetch", fetchMock);
        const onStage = vi.fn();
        const result = await new LiveRagGateway().runQuery(request, { signal: new AbortController().signal, onStage });
        expect(result).toEqual(run); expect(onStage).toHaveBeenCalledExactlyOnceWith(stage);
        expect(fetchMock.mock.calls[0][0]).toBe("/api/client/queries");
    });
    it("surfaces an unavailable client service", async () => {
        vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({ detail: "Client API is not configured." }, { status: 503 })));
        await expect(new LiveRagGateway().runQuery(request, { signal: new AbortController().signal, onStage: vi.fn() })).rejects.toThrow("not configured");
    });
    it("rejects a truncated stream", async () => {
        vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(": heartbeat\n\n")));
        await expect(new LiveRagGateway().runQuery(request, { signal: new AbortController().signal, onStage: vi.fn() })).rejects.toThrow("before a result");
    });
    it("surfaces a stage failure without saving a successful result", async () => {
        vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response('event: error\ndata: {"message":"Reranker unavailable"}\n\n')));
        await expect(new LiveRagGateway().runQuery(request, { signal: new AbortController().signal, onStage: vi.fn() })).rejects.toThrow("Reranker unavailable");
    });
    it("rejects malformed result metrics", () => {
        expect(() => parseRun({ ...run, totalLatencyMs: "fast" })).toThrow("metrics");
        expect(() => parseRun({ ...run, candidates: [{ id: "x", content: "text", vectorScore: Infinity }] })).toThrow("chunk");
    });
    it("passes cancellation through to fetch", async () => {
        const controller = new AbortController(); controller.abort();
        const fetchMock = vi.fn().mockRejectedValue(new DOMException("Canceled", "AbortError")); vi.stubGlobal("fetch", fetchMock);
        await expect(new LiveRagGateway().runQuery(request, { signal: controller.signal, onStage: vi.fn() })).rejects.toMatchObject({ name: "AbortError" });
        expect(fetchMock.mock.calls[0][1].signal).toBe(controller.signal);
    });
});
describe("client evaluation dataset", () => {
    const question = { id: "case1", question: "Refund window?", expectedSourceFile: "policy.md", goldAnchors: [{ phrase: "30 days" }] };
    it("accepts labeled evidence and abstention cases", () => {
        expect(parseEvaluationQuestions(JSON.stringify({ questions: [question, { id: "unknown", question: "Unknown?", expectedAbstention: true, goldAnchors: [] }] }))).toHaveLength(2);
    });
    it("rejects duplicate IDs and unlabeled answerable cases", () => {
        expect(() => parseEvaluationQuestions(JSON.stringify([question, question]))).toThrow("Duplicate");
        expect(() => parseEvaluationQuestions(JSON.stringify([{ ...question, goldAnchors: [] }]))).toThrow("anchors");
    });
    it("requires a source to disambiguate anchors and rejects wrong field types", () => {
        expect(() => parseEvaluationQuestions(JSON.stringify([{ ...question, expectedSourceFile: undefined }]))).toThrow("expectedSourceFile");
        expect(() => parseEvaluationQuestions(JSON.stringify([{ ...question, expectedAbstention: "true" }]))).toThrow("true or false");
    });
});
