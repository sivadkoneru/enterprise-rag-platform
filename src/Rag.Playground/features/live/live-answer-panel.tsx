"use client";
import { Hint } from "@/components/shared/hint";
import { Panel } from "@/components/shared/panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { downloadJson } from "@/lib/live/http";
import { Check, Copy, Download, FileSearch, Loader2 } from "lucide-react";
import { useLiveQuery } from "./use-live-query";

type Props = Pick<
    ReturnType<typeof useLiveQuery>,
    "run" | "request" | "running" | "copied" | "reveal" | "copy"
>;
export function LiveAnswerPanel({
    run,
    request,
    running,
    copied,
    reveal,
    copy,
}: Props) {
    return (
        <Panel
            title="Answer with source references"
            icon={<FileSearch size={15} className="text-primary" />}
            action={
                run && (
                    <Button
                        size="sm"
                        variant="ghost"
                        onClick={() =>
                            downloadJson(`query-${run.id}.json`, {
                                request,
                                run,
                            })
                        }
                    >
                        <Download size={12} />
                        Export run
                    </Button>
                )
            }
        >
            {running ? (
                <div role="status" className="space-y-4 py-8">
                    <p className="flex items-center gap-2 text-xs text-primary">
                        <Loader2 size={15} className="animate-spin" />
                        Executing against your environment…
                    </p>
                    <Skeleton className="h-3" />
                    <Skeleton className="h-3 w-5/6" />
                    <Skeleton className="h-3 w-2/3" />
                </div>
            ) : run ? (
                <>
                    <div className="mb-4 flex items-center gap-2 text-[10px] text-muted-foreground">
                        <Badge variant="outline">
                            {run.abstained
                                ? "Insufficient evidence"
                                : "Generated answer"}
                        </Badge>
                        <span className="mono ml-auto">
                            {run.totalLatencyMs.toFixed(1)} ms total
                        </span>
                    </div>
                    <p className="mb-3 break-words text-[10px] leading-5 text-muted-foreground">
                        {run.provider === "deterministic"
                            ? "Deterministic local execution"
                            : `Provider: ${run.provider ?? "not reported"}`}{" "}
                        · Chat: {run.chatModel ?? "not reported"} · Embedding:{" "}
                        {run.embeddingModel ?? "not reported"}
                        {run.traceId && (
                            <span className="block font-mono">
                                Trace {run.traceId}
                            </span>
                        )}
                    </p>
                    <div className="whitespace-pre-wrap text-[13px] leading-7">
                        {run.answer
                            .split(/(\[[^\]\n]+\])/g)
                            .map((part, index) => {
                                const identifier = part.startsWith("[")
                                    ? part.slice(1, -1)
                                    : null;
                                const citation = identifier
                                    ? run.citations.find(
                                          (c) =>
                                              c.valid &&
                                              (c.chunkId === identifier ||
                                                  String(c.number) ===
                                                      identifier),
                                      )
                                    : null;
                                return citation ? (
                                    <button
                                        key={index}
                                        aria-label={`Citation ${citation.number}: show source`}
                                        className="mx-1 rounded bg-accent px-1.5 font-mono text-[10px] text-primary"
                                        onClick={() => reveal(citation.chunkId)}
                                    >
                                        [{citation.number}]
                                    </button>
                                ) : (
                                    <span key={index}>{part}</span>
                                );
                            })}
                    </div>
                    {run.invalidCitations.length > 0 && (
                        <p
                            role="alert"
                            className="mt-4 rounded border border-destructive/30 p-3 text-xs text-destructive"
                        >
                            {run.invalidCitations.length} citation reference(s)
                            did not resolve to admitted context. Reference
                            validation does not establish factual support.
                        </p>
                    )}
                    <div className="mt-5 flex flex-wrap items-center gap-4 border-t pt-4 text-[10px] text-muted-foreground">
                        <span>
                            {run.citations.filter((c) => c.valid).length}{" "}
                            references resolved
                            <Hint text="A valid reference resolves to an admitted chunk. This check does not prove that the cited text supports every claim." />
                        </span>
                        <span>{run.context.length} context chunks</span>
                        <span>
                            {run.contextTokens} context tokens (est.) ·{" "}
                            {run.outputTokens} output tokens{" "}
                            {run.tokenUsageKind.startsWith("provider")
                                ? "(provider)"
                                : "(est.)"}
                            {run.totalTokens != null && (
                                <Hint
                                    text={`Provider total: ${run.totalTokens}; prompt: ${run.promptTokens ?? "unavailable"}. Context budgeting uses a separate characters ÷ 4 estimate.`}
                                />
                            )}
                        </span>
                        <Button
                            className="ml-auto"
                            size="sm"
                            variant="ghost"
                            onClick={() => void copy()}
                        >
                            {copied ? <Check size={12} /> : <Copy size={12} />}
                            {copied ? "Copied" : "Copy answer"}
                        </Button>
                    </div>
                </>
            ) : (
                <div className="py-14 text-center">
                    <FileSearch
                        className="mx-auto mb-4 text-primary"
                        size={28}
                    />
                    <h2 className="text-sm font-semibold">
                        Evidence from your environment
                    </h2>
                    <p className="mx-auto mt-2 max-w-sm text-xs leading-6 text-muted-foreground">
                        Select an indexed profile and run a question. Actual
                        source passages, scores, citations, and stage timings
                        will appear here.
                    </p>
                </div>
            )}
        </Panel>
    );
}
