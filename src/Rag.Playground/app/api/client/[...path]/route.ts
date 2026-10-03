import { randomBytes } from "node:crypto";
import { NextRequest } from "next/server";
import { clientConfiguration, privateLiveEnabled } from "@/lib/live/deployment";
import { readJsonBody, RequestBodyError } from "@/lib/live/request-body";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const readRoutes = /^(capabilities|configuration|readiness|corpora|corpora\/[a-zA-Z0-9_-]+\/profiles|profiles\/[a-zA-Z0-9_-]+\/(documents(\/[a-zA-Z0-9_-]+)?|chunks)|jobs(\/[a-zA-Z0-9_-]+)?|evaluations(\/[a-zA-Z0-9_-]+(\/export)?)?)$/;
const writeRoutes = /^(corpora|corpora\/[a-zA-Z0-9_-]+\/profiles|profiles\/[a-zA-Z0-9_-]+\/ingestions|jobs\/[a-zA-Z0-9_-]+\/(pause|resume|cancel)|queries|evaluations|checks\/(embedding|chat|reranker))$/;

async function proxy(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
    if (!privateLiveEnabled())
        return Response.json({ code: "CLIENT_DISABLED", detail: "Client access is disabled on this demo deployment." }, { status: 403 });
    const { path } = await context.params;
    const route = path.join("/");
    if (!(request.method === "GET" ? readRoutes : writeRoutes).test(route))
        return Response.json({ detail: "This client API route is not available." }, { status: 404 });
    let configuration: ReturnType<typeof clientConfiguration>;
    try { configuration = clientConfiguration(); }
    catch { return Response.json({ detail: "Private client API is not configured. Set a direct API URL, API key, and Playground origin on the server." }, { status: 503 }); }
    // Defense in depth for a private deployment, not visitor authentication.
    const origin = request.headers.get("origin");
    if (request.headers.get("host") !== configuration.origin.host ||
        request.headers.get("sec-fetch-site") === "cross-site" ||
        (origin !== null && origin !== configuration.origin.origin) ||
        (request.method === "POST" && origin === null))
        return Response.json({ detail: "Cross-origin client requests are not allowed." }, { status: 403 });
    try {
        const target = configuration.destination;
        target.pathname = `${target.pathname.replace(/\/$/, "")}/api/v1/${route}`;
        target.search = request.nextUrl.search;
        const body = request.method === "POST" ? await readJsonBody(request) : undefined;
        const traceId = randomBytes(16).toString("hex");
        const response = await fetch(target, {
            method: request.method,
            headers: {
                Accept: request.headers.get("accept") ?? "application/json",
                "Content-Type": "application/json",
                "X-API-Key": configuration.apiKey,
                traceparent: `00-${traceId}-${randomBytes(8).toString("hex")}-01`,
            },
            body,
            cache: "no-store",
            redirect: "manual",
            signal: AbortSignal.any([request.signal, AbortSignal.timeout(request.method === "GET" ? 15_000 : 300_000)]),
        });
        if (response.status >= 300 && response.status < 400) {
            await response.body?.cancel();
            return Response.json({ detail: "The client API returned a redirect. Configure its direct URL." }, { status: 502 });
        }
        if (!response.ok) {
            await response.body?.cancel();
            return Response.json({ detail: response.status === 429 ? "Client capacity reached. Retry later." : `Client request failed (${response.status}). Check the request and private server diagnostics.` }, {
                status: response.status,
                headers: { "Cache-Control": "no-store", ...(response.status === 429 ? { "Retry-After": "60" } : {}) },
            });
        }
        return new Response(response.body, {
            status: response.status,
            headers: {
                "Content-Type": response.headers.get("content-type") ?? "application/json",
                "Cache-Control": "no-store",
                "X-Content-Type-Options": "nosniff",
                "X-Accel-Buffering": "no",
                "X-Request-ID": traceId,
            },
        });
    } catch (error) {
        if (error instanceof RequestBodyError) return Response.json({ detail: error.message }, { status: error.status });
        console.error("Client API transport failed", { canceled: request.signal.aborted });
        return Response.json({ detail: request.signal.aborted ? "Request canceled." : "The client API could not be reached or timed out. Check service status and the configured endpoint." }, { status: 502 });
    }
}
export const GET = proxy;
export const POST = proxy;
