import { describe, expect, it } from "vitest";
import { readJsonBody } from "@/lib/live/request-body";

describe("bounded JSON request reader", () => {
    it("rejects a chunked body before reading the rest", async () => {
        let canceled = false;
        const body = new ReadableStream({ start(controller) { controller.enqueue(new Uint8Array(21)); }, cancel() { canceled = true; } });
        const request = new Request("http://localhost", { method: "POST", body, headers: { "content-type": "application/json" }, duplex: "half" } as RequestInit);
        await expect(readJsonBody(request, 20)).rejects.toMatchObject({ status: 413 });
        expect(canceled).toBe(true);
    });
    it.each([["text/plain", "{}", 415], ["application/json", "{broken", 400]])("rejects %s body %s", async (type, body, status) => {
        await expect(readJsonBody(new Request("http://localhost", { method: "POST", body, headers: { "content-type": type } }))).rejects.toMatchObject({ status });
    });
});
