"use client";
import { Hint } from "@/components/shared/hint";
import { Panel } from "@/components/shared/panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
    Check,
    CheckCheck,
    Copy,
    FileSearch,
    FileText,
    Layers3,
    Loader2,
    ShieldCheck,
    Sparkles,
} from "lucide-react";
import { usePlayground } from "./use-playground";

type Props = Pick<
    ReturnType<typeof usePlayground>,
    "run" | "running" | "copied" | "revealSource" | "copyAnswer"
>;
export function DemoAnswerPanel({
    run,
    running,
    copied,
    revealSource,
    copyAnswer,
}: Props) {
    return (
        <Panel
            title="Answer with source references"
            icon={<Sparkles size={15} className="text-primary" />}
            action={
                <Badge
                    variant="outline"
                    className="gap-1.5 rounded text-[9px] font-normal"
                >
                    {run ? (
                        <>
                            <CheckCheck size={11} />
                            Prepared demo answer
                        </>
                    ) : (
                        <>
                            <span className="h-1.5 w-1.5 rounded-full bg-primary" />
                            Demo Mode
                        </>
                    )}
                </Badge>
            }
            bodyClassName={run ? "!p-0" : ""}
        >
            {running ? (
                <div
                    role="status"
                    aria-label="Generating grounded answer"
                    className="space-y-3 py-8"
                >
                    <div className="mb-6 flex items-center gap-2 text-xs text-primary">
                        <Loader2 size={15} className="animate-spin" />
                        Retrieving and validating evidence…
                    </div>
                    <Skeleton className="h-3 w-full" />
                    <Skeleton className="h-3 w-[92%]" />
                    <Skeleton className="h-3 w-[96%]" />
                    <Skeleton className="h-3 w-[70%]" />
                </div>
            ) : run ? (
                <>
                    <div className="p-5">
                        <div className="mb-4 flex items-center gap-2 text-[10px] text-muted-foreground">
                            <span className="status-dot" />
                            {run.abstained
                                ? "Abstained · insufficient evidence"
                                : "Prepared answer from cited excerpts"}
                            <span className="mono ml-auto text-[9px]">
                                {run.id.slice(0, 8)}
                            </span>
                        </div>
                        <div className="space-y-3 text-[13px] leading-[2]">
                            {run.answer.map((segment, index) => (
                                <p key={index}>
                                    {segment.text}
                                    {segment.citationNumber && (
                                        <button
                                            className="mx-1 inline-flex h-5 min-w-5 items-center justify-center rounded bg-accent px-1 font-mono text-[10px] font-semibold text-primary hover:ring-1 hover:ring-primary"
                                            onClick={() =>
                                                revealSource(
                                                    segment.citationNumber!,
                                                )
                                            }
                                            aria-label={`Citation ${segment.citationNumber}: show source`}
                                        >
                                            {segment.citationNumber}
                                        </button>
                                    )}
                                </p>
                            ))}
                        </div>
                    </div>
                    <div className="flex flex-wrap items-center justify-between gap-3 border-t bg-muted/25 px-5 py-3">
                        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-[10px] text-muted-foreground">
                            <span className="flex items-center gap-1.5">
                                <ShieldCheck
                                    size={12}
                                    className="text-[var(--success)]"
                                />
                                Evidence coverage:{" "}
                                <strong className="font-medium text-foreground">
                                    {run.evidenceStatements} cited statements
                                </strong>
                                <Hint text={run.evidenceReason} />
                            </span>
                            <span>
                                Citations:{" "}
                                <span className="mono text-foreground">
                                    {run.citations.length}
                                </span>
                            </span>
                            <span>
                                Context:{" "}
                                <span className="mono text-foreground">
                                    {run.context.length} chunks
                                </span>
                            </span>
                            <span className="flex items-center gap-1">
                                Tokens:{" "}
                                <span className="mono text-foreground">
                                    {run.contextTokens + run.outputTokens}
                                </span>
                                <Hint
                                    text={`Estimated from characters ÷ 4. Context: ${run.contextTokens}; output: ${run.outputTokens}.`}
                                />
                            </span>
                        </div>
                        <Button
                            variant="ghost"
                            size="sm"
                            className="h-7 px-0 text-[10px]"
                            onClick={() => void copyAnswer()}
                        >
                            {copied ? <Check size={11} /> : <Copy size={11} />}
                            {copied ? "Copied" : "Copy answer"}
                        </Button>
                    </div>
                </>
            ) : (
                <div className="flex min-h-[280px] flex-col items-center justify-center text-center">
                    <div className="relative mb-5 flex h-14 w-14 items-center justify-center rounded-2xl border border-primary/15 bg-accent/60">
                        <FileSearch
                            size={26}
                            strokeWidth={1.3}
                            className="text-primary"
                        />
                        <span className="absolute -bottom-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full border-2 border-card bg-[var(--success-bg)] text-[var(--success)]">
                            <Check size={10} />
                        </span>
                    </div>
                    <h3 className="text-[15px] font-semibold tracking-tight">
                        Answers start with evidence.
                    </h3>
                    <p className="mt-2 max-w-[310px] text-xs leading-6 text-muted-foreground">
                        Run a query to see a grounded answer, inspect its
                        sources, and follow the pipeline from retrieval to
                        response.
                    </p>
                    <div className="mt-6 flex flex-wrap justify-center gap-4 text-[10px] text-muted-foreground">
                        <span className="flex items-center gap-1.5">
                            <FileText size={12} />
                            Linked citations
                        </span>
                        <span className="flex items-center gap-1.5">
                            <Layers3 size={12} />
                            Ranked context
                        </span>
                        <span className="flex items-center gap-1.5">
                            <ShieldCheck size={12} />
                            Evidence validation
                        </span>
                    </div>
                </div>
            )}
        </Panel>
    );
}
