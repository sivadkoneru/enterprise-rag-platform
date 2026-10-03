/** Incremental UTF-8 decoder with a bounded incomplete frame and explicit cancellation. */
export async function* readEvents(body: ReadableStream<Uint8Array>, signal: AbortSignal, maximum = 2_000_000) {
    const reader = body.getReader();
    const decoder = new TextDecoder("utf-8", { fatal: true });
    let buffer = "";
    const cancel = () => { void reader.cancel().catch(() => {}); };
    signal.addEventListener("abort", cancel, { once: true });
    try {
        while (true) {
            signal.throwIfAborted();
            const { value, done } = await reader.read();
            signal.throwIfAborted();
            buffer = (buffer + decoder.decode(value, { stream: !done })).replace(/\r\n/g, "\n");
            let boundary: number;
            while ((boundary = buffer.indexOf("\n\n")) !== -1) {
                if (boundary > maximum) throw new Error("Query stream frame exceeded the supported size.");
                const lines = buffer.slice(0, boundary).split("\n");
                buffer = buffer.slice(boundary + 2);
                const event = lines.find(line => line.startsWith("event:"))?.slice(6).trim();
                const data = lines.filter(line => line.startsWith("data:")).map(line => line.slice(5).trimStart()).join("\n");
                if (data) yield { event, data };
            }
            if (buffer.length > maximum) throw new Error("Query stream frame exceeded the supported size.");
            if (done) {
                if (buffer.trim()) throw new Error("The query stream ended with an incomplete event.");
                break;
            }
        }
    } finally {
        signal.removeEventListener("abort", cancel);
        await reader.cancel().catch(() => {});
        reader.releaseLock();
    }
}
