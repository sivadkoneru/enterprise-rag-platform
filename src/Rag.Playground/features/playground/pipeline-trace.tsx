"use client";
import { useState } from "react";
import {
    Check,
    ChevronDown,
    Circle,
    CircleAlert,
    Clock3,
    Loader2,
    Minus,
    Workflow,
} from "lucide-react";
import { Panel } from "@/components/shared/panel";
import { Badge } from "@/components/ui/badge";
import type { TraceStage } from "@/lib/contracts";
import { cn } from "@/lib/utils";
const colors = [
    "#a0a4b7",
    "#9b9bcf",
    "#7e80db",
    "#6ba8bc",
    "#5ba991",
    "#c6a26c",
    "#6863d9",
    "#aa86bd",
];

export function PipelineTrace({
    stages,
    running = false,
    simulated = true,
    totalLatencyMs,
}: {
    stages: TraceStage[];
    running?: boolean;
    simulated?: boolean;
    totalLatencyMs?: number;
}) {
    const [expanded, setExpanded] = useState<string | null>(null);
    const stageTotal = stages.reduce((sum, stage) => sum + stage.durationMs, 0);
    const total = totalLatencyMs ?? stageTotal;
    const overhead = Math.max(0, total - stageTotal);
    const complete =
        stages.length > 0 &&
        stages.every(
            (stage) =>
                stage.status === "complete" || stage.status === "skipped",
        );
    const selected = stages.find((stage) => stage.id === expanded);
    return (
        <Panel
            title="Pipeline Trace"
            icon={<Workflow size={15} className="text-primary" />}
            action={
                <Badge variant="outline" className="text-[9px] font-normal">
                    {running
                        ? "Executing"
                        : complete
                          ? "Complete"
                                        : stages.some(stage => stage.status === "failed") ? "Failed" : stages.some(stage => stage.status === "canceled") ? "Canceled" : "Awaiting query"}
                </Badge>
            }
        >
            <div className="mb-4 flex items-center justify-between">
                <p className="text-[11px] text-muted-foreground">
                    Stage events, not model-token streaming.
                </p>
                <span className="mono text-[11px]">
                    {complete ? `${total} ms` : "—"}
                    <span className="ml-1.5 font-sans text-[10px] text-muted-foreground">
                        {simulated ? "scripted duration" : "measured stages"}
                    </span>
                </span>
            </div>
            {stages.length === 0 ? (
                <div className="rounded-lg border border-dashed bg-muted/30 px-4 py-7 text-center text-xs text-muted-foreground">
                    <Clock3 size={19} className="mx-auto mb-2 opacity-60" />
                    Run a query to inspect stage timing and diagnostics.
                </div>
            ) : (
                <>
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                        {stages.map((stage, index) => {
                            const Icon =
                                stage.status === "complete"
                                    ? Check
                                    : stage.status === "running"
                                      ? Loader2
                                      : stage.status === "failed"
                                        ? CircleAlert
                                        : stage.status === "skipped"
                                          ? Minus
                                          : Circle;
                            return (
                                <button
                                    key={stage.id}
                                    aria-expanded={expanded === stage.id}
                                    onClick={() =>
                                        setExpanded(
                                            expanded === stage.id
                                                ? null
                                                : stage.id,
                                        )
                                    }
                                    className={cn(
                                        "min-w-0 rounded-md border px-2.5 py-3 text-left transition-colors hover:bg-muted",
                                        expanded === stage.id &&
                                            "border-primary bg-accent/30",
                                        stage.status === "running" &&
                                            "border-primary/50 bg-accent/40",
                                    )}
                                >
                                    <div className="mb-2 flex items-center justify-between">
                                        <span className="mono text-[9px] text-muted-foreground">
                                            {String(index + 1).padStart(2, "0")}
                                        </span>
                                        <Icon
                                            size={12}
                                            className={cn(
                                                stage.status === "running" &&
                                                    "animate-spin text-primary",
                                                stage.status === "complete" &&
                                                    "text-[var(--success)]",
                                                stage.status === "failed" &&
                                                    "text-destructive",
                                            )}
                                        />
                                    </div>
                                    <span
                                        className="block truncate text-[10px] font-medium"
                                        title={stage.name}
                                    >
                                        {stage.name}
                                    </span>
                                    <span className="mono mt-1 block text-xs">
                                        {stage.status === "complete"
                                            ? `${stage.durationMs} ms`
                                            : stage.status === "skipped"
                                              ? "Skipped"
                                              : stage.status === "running"
                                                ? "Running"
                                                : stage.status === "failed"
                                                  ? "Failed"
                                                  : stage.status === "canceled" ? "Canceled" : "Queued"}
                                    </span>
                                    <span
                                        className="mt-1.5 block truncate text-[9px] text-muted-foreground"
                                        title={stage.detail}
                                    >
                                        {stage.detail}
                                    </span>
                                </button>
                            );
                        })}
                    </div>
                    {selected && (
                        <div className="mt-3 rounded-lg border bg-muted/40 p-4">
                            <div className="mb-3 flex items-center justify-between text-xs font-medium">
                                {selected.name} diagnostics
                                <ChevronDown size={12} />
                            </div>
                            <dl className="space-y-2">
                                {Object.entries(selected.diagnostics).map(
                                    ([key, value]) => (
                                        <div
                                            key={key}
                                            className="flex flex-wrap justify-between gap-2 text-[10px]"
                                        >
                                            <dt className="text-muted-foreground">
                                                {key}
                                            </dt>
                                            <dd className="mono max-w-full break-words text-right">
                                                {String(value)}
                                            </dd>
                                        </div>
                                    ),
                                )}
                            </dl>
                        </div>
                    )}
                    {complete && (
                        <div className="mt-5 border-t pt-4">
                            <div className="mb-2 flex justify-between text-[10px]">
                                <span className="text-muted-foreground">
                                    Latency breakdown
                                </span>
                                <span className="mono">
                                    Total latency: {total} ms
                                </span>
                            </div>
                            <div
                                className="flex h-2.5 overflow-hidden rounded-sm"
                                role="img"
                                aria-label={`${simulated ? "Simulated" : "Measured"} latency, ${total} milliseconds. Stage values are shown above.`}
                            >
                                {stages
                                    .filter((stage) => stage.durationMs > 0)
                                    .map((stage) => (
                                        <div
                                            key={stage.id}
                                            title={`${stage.name}: ${stage.durationMs} ms`}
                                            style={{
                                                width: `${(stage.durationMs / total) * 100}%`,
                                                background:
                                                    colors[
                                                        stages.indexOf(stage)
                                                    ],
                                            }}
                                        />
                                    ))}
                                {overhead > 0 && <div title={`Orchestration and rounding: ${overhead} ms`} style={{ width: `${overhead / Math.max(1, total) * 100}%`, background: "#a0a4b7" }} />}
                            </div>
                            <div className="mt-3 flex flex-wrap gap-x-3 gap-y-2">
                                {overhead > 0 && <span className="text-[9px] text-muted-foreground">Orchestration / rounding: {overhead} ms</span>}
                                {stages
                                    .filter((stage) => stage.durationMs > 0)
                                    .map((stage) => (
                                        <span
                                            key={stage.id}
                                            className="flex items-center gap-1.5 text-[9px] text-muted-foreground"
                                        >
                                            <span
                                                className="h-1.5 w-1.5 rounded-sm"
                                                style={{
                                                    background:
                                                        colors[
                                                            stages.indexOf(
                                                                stage,
                                                            )
                                                        ],
                                                }}
                                            />
                                            {stage.name}
                                        </span>
                                    ))}
                            </div>
                        </div>
                    )}
                </>
            )}
        </Panel>
    );
}
