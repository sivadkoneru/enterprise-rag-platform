import type { QueryRequest, QueryRun } from "@/lib/contracts";
import { createRun } from "@/lib/demo/gateway";
const key = "rag-lab-history-v1";
const event = "rag-lab-history-changed";
export function subscribeHistory(onChange: () => void) {
    window.addEventListener(event, onChange);
    return () => window.removeEventListener(event, onChange);
}
export function historySnapshot() {
    try {
        return sessionStorage.getItem(key) ?? "";
    } catch {
        return "";
    }
}
export function readHistory(snapshot: string): QueryRun[] {
    try {
        const parsed: unknown = JSON.parse(snapshot || "[]");
        if (!Array.isArray(parsed)) return [];
        return parsed.slice(0, 20).flatMap((item: unknown) => {
            try {
                if (
                    typeof item !== "object" ||
                    item === null ||
                    !("request" in item) ||
                    !("id" in item) ||
                    typeof item.id !== "string" ||
                    !("createdAt" in item) ||
                    typeof item.createdAt !== "string" ||
                    !Number.isFinite(Date.parse(item.createdAt))
                )
                    return [];
                // Rebuild deterministic results from a validated request rather than trusting stored HTML or scores.
                const run = createRun(item.request as QueryRequest);
                run.id = item.id;
                run.createdAt = item.createdAt;
                run.trace.forEach((stage) => {
                    stage.status =
                        stage.id === "reranking" && !run.request.config.reranker
                            ? "skipped"
                            : "complete";
                });
                return [run];
            } catch {
                return [];
            }
        });
    } catch {
        return [];
    }
}
export function saveHistory(run: QueryRun) {
    try {
        sessionStorage.setItem(
            key,
            JSON.stringify(
                [
                    run,
                    ...readHistory(historySnapshot()).filter(
                        (item) => item.id !== run.id,
                    ),
                ].slice(0, 20),
            ),
        );
        window.dispatchEvent(new Event(event));
        return true;
    } catch {
        return false;
    }
}
