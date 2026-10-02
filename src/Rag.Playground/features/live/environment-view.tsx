"use client";
import { useEnvironment } from "@/components/shell/environment";
import { LiveQueryPage } from "./live-query-page";
import { LiveEvaluationPage } from "./live-evaluation-page";
import { LiveArchitecture } from "./live-architecture";

export function EnvironmentView({ page, children }: { page: "playground" | "retrieval" | "evaluation" | "architecture"; children: React.ReactNode }) {
    const { mode } = useEnvironment();
    if (mode === "demo") return children;
    if (page === "evaluation") return <LiveEvaluationPage />;
    if (page === "architecture") return <LiveArchitecture />;
    return <LiveQueryPage key={page} inspector={page === "retrieval"} />;
}
