import { describe, it, expect } from "vitest";
import { readEvents } from "@/lib/live/sse";

async function collect(bytes: Uint8Array[], limit?: number) {
    const body = new ReadableStream<Uint8Array>({ start(controller) { bytes.forEach(part => controller.enqueue(part)); controller.close(); } });
    const events = [];
    for await (const event of readEvents(body, new AbortController().signal, limit)) events.push(event);
    return events;
}
describe("stage event stream", () => {
    it("decodes fragmented Unicode and split CRLF delimiters", async () => {
        const bytes = new TextEncoder().encode('event: result\r\ndata: {"answer":"café 🧪"}\r\n\r\n');
        const events = await collect([...bytes].map(byte => new Uint8Array([byte])));
        expect(events).toEqual([{ event: "result", data: '{"answer":"café 🧪"}' }]);
    });
    it("rejects oversized incomplete frames", async () => {
        await expect(collect([new TextEncoder().encode("data: oversized")], 8)).rejects.toThrow("size");
    });
    it("rejects a truncated final event", async () => {
        await expect(collect([new TextEncoder().encode('event: result\ndata: {}')])).rejects.toThrow("incomplete");
    });
});
