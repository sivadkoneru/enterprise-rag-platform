import { NextRequest } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const readRoutes = /^(capabilities|configuration|readiness|corpora|corpora\/[a-zA-Z0-9_-]+\/profiles|profiles\/[a-zA-Z0-9_-]+\/(documents(\/[a-zA-Z0-9_-]+)?|chunks)|jobs(\/[a-zA-Z0-9_-]+)?|evaluations(\/[a-zA-Z0-9_-]+(\/export)?)?)$/;
const writeRoutes = /^(corpora|corpora\/[a-zA-Z0-9_-]+\/profiles|profiles\/[a-zA-Z0-9_-]+\/ingestions|jobs\/[a-zA-Z0-9_-]+\/(pause|resume|cancel)|queries|evaluations|checks\/(embedding|chat|reranker))$/;

async function proxy(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
    const { path } = await context.params;
    const route = path.join("/");
    if (!(request.method === "GET" ? readRoutes : writeRoutes).test(route))
        return Response.json({ detail: "This client API route is not available." }, { status: 404 });
    const destination = process.env.RAG_API_URL;
    if (!destination) return Response.json({ detail: "Client API is not configured. Download the setup files, launch the local stack, and set RAG_API_URL on the Playground server." }, { status: 503 });

    // Do not let another website use a localhost instance as an authenticated API proxy.
    const origin = request.headers.get("origin");
    const host = request.headers.get("host");
    let originMatches = true;
    try { originMatches = !origin || new URL(origin).host === host; }
    catch { originMatches = false; }
    if (request.headers.get("sec-fetch-site") === "cross-site" || !originMatches)
        return Response.json({ detail: "Cross-origin client requests are not allowed." }, { status: 403 });
    const length = Number(request.headers.get("content-length") ?? 0);
    if (length > 2_000_000) return Response.json({ detail: "Request exceeds the 2 MB limit." }, { status: 413 });
    try {
        const target = new URL(destination);
        if (!["http:", "https:"].includes(target.protocol) || target.username || target.password)
            return Response.json({ detail: "RAG_API_URL must be an HTTP endpoint without embedded credentials." }, { status: 503 });
        target.pathname = `${target.pathname.replace(/\/$/, "")}/api/v1/${route}`;
        target.search = request.nextUrl.search;
        const body = request.method === "POST" ? await request.text() : undefined;
        if (body && new TextEncoder().encode(body).length > 2_000_000)
            return Response.json({ detail: "Request exceeds the 2 MB limit." }, { status: 413 });
        const response = await fetch(target, {
            method: request.method,
            headers: {
                Accept: request.headers.get("accept") ?? "application/json",
                "Content-Type": "application/json",
                ...(process.env.RAG_API_KEY ? { "X-API-Key": process.env.RAG_API_KEY } : {}),
            },
            body,
            cache: "no-store",
            redirect: "manual",
            signal: AbortSignal.any([request.signal, AbortSignal.timeout(300_000)]),
        });
        if (response.status >= 300 && response.status < 400)
            return Response.json({ detail: "The client API returned a redirect. Configure its direct URL." }, { status: 502 });
        return new Response(response.body, {
            status: response.status,
            headers: {
                "Content-Type": response.headers.get("content-type") ?? "application/json",
                "Cache-Control": "no-store",
                "X-Content-Type-Options": "nosniff",
                "X-Accel-Buffering": "no",
            },
        });
    } catch (error) {
        const cause = error instanceof Error && error.cause && typeof error.cause === "object" && "code" in error.cause ? String(error.cause.code) : "unavailable";
        console.error("Client API transport failed", { code: cause });
        return Response.json({ detail: request.signal.aborted ? "Request canceled." : "The client API could not be reached or timed out. Check service status and the configured endpoint." }, { status: 502 });
    }
}
export const GET = proxy;
export const POST = proxy;
