import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { GET, POST } from "@/app/api/client/[...path]/route";

afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs(); });
const context = (path: string) => ({ params: Promise.resolve({ path: path.split("/") }) });
const request = (path: string, init?: RequestInit) => new NextRequest(`http://localhost:3000/api/client/${path}`, { ...init, signal: init?.signal ?? undefined, headers: { host: "localhost:3000", ...init?.headers } });

describe("server-only client proxy", () => {
    it("requires server configuration without making a network request", async () => {
        vi.stubEnv("RAG_API_URL", ""); const fetchMock = vi.fn(); vi.stubGlobal("fetch", fetchMock);
        const response = await GET(request("capabilities"), context("capabilities"));
        expect(response.status).toBe(503); expect(fetchMock).not.toHaveBeenCalled();
    });
    it("rejects unlisted routes and cross-origin requests", async () => {
        vi.stubEnv("RAG_API_URL", "http://api:8080"); const fetchMock = vi.fn(); vi.stubGlobal("fetch", fetchMock);
        expect((await GET(request("arbitrary"), context("arbitrary"))).status).toBe(404);
        expect((await POST(request("queries", { method: "POST", headers: { origin: "https://external.example" } }), context("queries"))).status).toBe(403);
        expect((await GET(request("corpora", { headers: { origin: "malformed" } }), context("corpora"))).status).toBe(403);
        expect(fetchMock).not.toHaveBeenCalled();
    });
    it("forwards only the configured API key and never returns it", async () => {
        vi.stubEnv("RAG_API_URL", "http://api:8080"); vi.stubEnv("RAG_API_KEY", "unit-test-placeholder");
        const fetchMock = vi.fn().mockResolvedValue(Response.json({ ready: true })); vi.stubGlobal("fetch", fetchMock);
        const response = await GET(request("readiness"), context("readiness"));
        expect(fetchMock.mock.calls[0][0].toString()).toBe("http://api:8080/api/v1/readiness");
        expect(fetchMock.mock.calls[0][1].headers["X-API-Key"]).toBe("unit-test-placeholder");
        expect(response.headers.get("X-API-Key")).toBeNull();
        expect(await response.text()).not.toContain("unit-test-placeholder");
        expect(response.headers.get("Cache-Control")).toBe("no-store");
    });
    it("refuses redirects so credentials cannot follow them", async () => {
        vi.stubEnv("RAG_API_URL", "http://api:8080");
        const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 302, headers: { location: "https://external.example" } })); vi.stubGlobal("fetch", fetchMock);
        expect((await GET(request("readiness"), context("readiness"))).status).toBe(502);
        expect(fetchMock.mock.calls[0][1].redirect).toBe("manual");
    });
    it("bounds request size and suppresses upstream exception details", async () => {
        vi.stubEnv("RAG_API_URL", "http://api:8080");
        vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("upstream-sensitive-detail")));
        expect((await POST(request("queries", { method: "POST", headers: { "content-length": "2000001" } }), context("queries"))).status).toBe(413);
        const response = await GET(request("readiness"), context("readiness"));
        expect(response.status).toBe(502); expect(await response.text()).not.toContain("upstream-sensitive-detail");
    });
});
