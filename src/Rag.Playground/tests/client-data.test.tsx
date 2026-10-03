// @vitest-environment jsdom
import { act, renderHook, waitFor, cleanup } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { useClientData } from "@/features/live/use-client-data";
import { liveGateway } from "@/lib/live/gateway";
import type { IndexProfile } from "@/lib/live/contracts";

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

it("a late profile response cannot replace a newer corpus selection", async () => {
    vi.spyOn(liveGateway, "listCorpora").mockResolvedValue([{ id: "a", name: "A", description: "" }, { id: "b", name: "B", description: "" }]);
    vi.spyOn(liveGateway, "capabilities").mockResolvedValue({ embeddingModel: "test", embeddingDimensions: 2, documentStore: "memory", vectorStore: "memory", jobStore: "memory", hybrid: true, reranker: false });
    let resolveOld!: (profiles: IndexProfile[]) => void;
    const oldResponse = new Promise<IndexProfile[]>(resolve => { resolveOld = resolve; });
    const profile = (id: string, corpusId: string): IndexProfile => ({ id, corpusId, name: id, strategy: "recursive", chunkSize: 800, chunkOverlap: 120, embeddingModel: "test", embeddingDimensions: 2, status: "ready", chunkCount: 1, documentCount: 1, createdAt: "2026-01-01" });
    vi.spyOn(liveGateway, "listProfiles").mockImplementation(id => id === "a" ? oldResponse : Promise.resolve([profile("new", "b")]));
    const { result } = renderHook(() => useClientData());
    await waitFor(() => expect(result.current.corpusId).toBe("a"));
    act(() => result.current.setCorpusId("b"));
    await waitFor(() => expect(result.current.profileId).toBe("new"));
    await act(async () => resolveOld([profile("stale", "a")]));
    expect(result.current.profileId).toBe("new");
    expect(result.current.profiles.map(item => item.id)).toEqual(["new"]);
});
