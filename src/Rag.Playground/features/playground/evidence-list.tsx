"use client";
import { CheckCheck, ChevronDown, FileText, ArrowUpRight } from "lucide-react";
import type { RetrievedChunk } from "@/lib/contracts";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export function EvidenceList({
    chunks,
    expandedIds,
    onToggle,
    highlightedId,
    onDocument,
}: {
    chunks: RetrievedChunk[];
    expandedIds: string[];
    onToggle: (id: string) => void;
    highlightedId: string | null;
    onDocument: (id: string) => void;
}) {
    return (
        <div className="space-y-2.5">
            {chunks.map((chunk) => {
                const expanded = expandedIds.includes(chunk.id);
                return (
                    <article
                        key={chunk.id}
                        id={`source-${chunk.id}`}
                        tabIndex={-1}
                        className="evidence-card"
                        data-highlighted={chunk.id === highlightedId}
                    >
                        <button
                            onClick={() => onToggle(chunk.id)}
                            className="block w-full p-3.5 text-left"
                            aria-expanded={expanded}
                            aria-label={`${expanded ? "Collapse" : "Expand"} ${chunk.filename}, chunk ${chunk.index}`}
                        >
                            <div className="flex items-center gap-2">
                                <span
                                    className={cn(
                                        "mono flex h-6 w-6 shrink-0 items-center justify-center rounded text-[10px]",
                                        chunk.inContext
                                            ? "bg-accent text-primary"
                                            : "bg-muted text-muted-foreground",
                                    )}
                                >
                                    {chunk.isNeighbor
                                        ? "+"
                                        : `#${chunk.rankAfter}`}
                                </span>
                                <FileText
                                    size={13}
                                    className="shrink-0 text-muted-foreground"
                                />
                                <span className="min-w-0 truncate text-[11px] font-medium">
                                    {chunk.filename}
                                </span>
                                <span className="mono ml-auto shrink-0 text-xs font-medium">
                                    {chunk.score.toFixed(2)}
                                </span>
                                <ChevronDown
                                    size={13}
                                    className={cn(
                                        "shrink-0 text-muted-foreground transition-transform",
                                        expanded && "rotate-180",
                                    )}
                                />
                            </div>
                            <div className="mt-3 flex items-center justify-between gap-2">
                                <h3 className="text-[11px] font-medium">
                                    {chunk.section}
                                </h3>
                                <Badge
                                    variant="outline"
                                    className={cn(
                                        "shrink-0 gap-1 rounded text-[9px] font-normal",
                                        chunk.inContext
                                            ? "border-transparent bg-[var(--success-bg)] text-[var(--success)]"
                                            : "text-muted-foreground",
                                    )}
                                >
                                    {chunk.inContext ? (
                                        <>
                                            <CheckCheck size={10} />
                                            {chunk.isNeighbor
                                                ? "Neighbor"
                                                : "In context"}
                                        </>
                                    ) : (
                                        "Excluded"
                                    )}
                                </Badge>
                            </div>
                            <p
                                className={cn(
                                    "mt-2 text-[11px] leading-[1.8] text-muted-foreground",
                                    !expanded && "line-clamp-2",
                                )}
                            >
                                {chunk.content}
                            </p>
                            {!chunk.inContext && (
                                <p className="mt-2 text-[10px] text-[var(--warning)]">
                                    {chunk.exclusionReason}
                                </p>
                            )}
                        </button>
                        {expanded && (
                            <div className="border-t px-3.5 pb-3.5 pt-3">
                                <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-[10px] sm:grid-cols-4">
                                    {[
                                        [
                                            "Illustrative retrieval score",
                                            chunk.vectorScore.toFixed(2),
                                        ],
                                        [
                                            "Illustrative reranker score",
                                            chunk.rerankerScore?.toFixed(2) ??
                                                "Disabled",
                                        ],
                                        ["Chunk", String(chunk.index)],
                                        ["Est. tokens", String(chunk.tokens)],
                                    ].map(([label, value]) => (
                                        <div key={label}>
                                            <dt className="text-muted-foreground">
                                                {label}
                                            </dt>
                                            <dd className="mono mt-1 font-medium">
                                                {value}
                                            </dd>
                                        </div>
                                    ))}
                                </dl>
                                <div className="mt-3 flex items-center justify-between">
                                    <span className="text-[9px] text-muted-foreground">
                                        {chunk.isNeighbor
                                            ? "Adjacent source context"
                                            : `Rank ${chunk.rankBefore} → ${chunk.rankAfter} · Simulated scores`}
                                    </span>
                                    <Button
                                        variant="ghost"
                                        size="sm"
                                        className="h-7 px-2 text-[10px]"
                                        onClick={() =>
                                            onDocument(chunk.documentId)
                                        }
                                    >
                                        View document
                                        <ArrowUpRight size={11} />
                                    </Button>
                                </div>
                            </div>
                        )}
                        {!expanded && (
                            <div className="px-3.5 pb-2">
                                <Button
                                    variant="ghost"
                                    size="sm"
                                    className="h-6 p-0 text-[10px] text-primary"
                                    onClick={() => onToggle(chunk.id)}
                                >
                                    View full chunk
                                </Button>
                            </div>
                        )}
                    </article>
                );
            })}
        </div>
    );
}
