/** Server-owned deployment policy. Never derive authority from browser state. */
export function privateLiveEnabled(): boolean {
    return process.env.RAG_PLAYGROUND_MODE === "private-live";
}

export function clientConfiguration() {
    const destination = new URL(process.env.RAG_API_URL ?? "");
    const origin = new URL(process.env.RAG_PLAYGROUND_ORIGIN ?? "");
    for (const url of [destination, origin]) {
        if (!["http:", "https:"].includes(url.protocol) || url.username || url.password || url.search || url.hash)
            throw new Error("Invalid server endpoint configuration.");
    }
    if (origin.pathname !== "/" || !process.env.RAG_API_KEY?.trim())
        throw new Error("Private live mode requires an origin and API key.");
    return { destination, origin, apiKey: process.env.RAG_API_KEY };
}
