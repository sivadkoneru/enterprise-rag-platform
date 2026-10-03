export class RequestBodyError extends Error {
    constructor(readonly status: number, message: string) { super(message); }
}

/** Enforces the byte limit before buffering the entire body, including chunked requests. */
export async function readJsonBody(request: Request, maximum = 2_000_000): Promise<string> {
    if (request.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json")
        throw new RequestBodyError(415, "Use application/json.");
    const declared = request.headers.get("content-length");
    if (declared !== null && (!/^\d+$/.test(declared) || Number(declared) > maximum))
        throw new RequestBodyError(413, "Request exceeds the 2 MB limit.");
    if (!request.body) throw new RequestBodyError(400, "Provide a JSON body.");
    const reader = request.body.getReader();
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(15_000)]);
    const abort = () => { void reader.cancel().catch(() => {}); };
    signal.addEventListener("abort", abort, { once: true });
    const decoder = new TextDecoder("utf-8", { fatal: true });
    let bytes = 0;
    let body = "";
    try {
        while (true) {
            signal.throwIfAborted();
            const { done, value } = await reader.read();
            signal.throwIfAborted();
            if (done) break;
            bytes += value.byteLength;
            if (bytes > maximum) throw new RequestBodyError(413, "Request exceeds the 2 MB limit.");
            body += decoder.decode(value, { stream: true });
        }
        body += decoder.decode();
        JSON.parse(body);
        return body;
    } catch (error) {
        if (error instanceof RequestBodyError) throw error;
        if (signal.aborted) throw new RequestBodyError(408, "Request body timed out or was canceled.");
        throw new RequestBodyError(400, "Provide valid UTF-8 JSON.");
    } finally {
        signal.removeEventListener("abort", abort);
        await reader.cancel().catch(() => {});
        reader.releaseLock();
    }
}
