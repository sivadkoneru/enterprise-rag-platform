"use client";
import { CheckCheck, ChevronDown, FileText } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { LiveChunk } from "@/lib/live/contracts";

export function score(value: number | null | undefined) { return value == null ? "—" : value.toFixed(4); }
export function LiveEvidence({ chunks, expanded, selected, onToggle, onDocument }: { chunks: LiveChunk[]; expanded: string[]; selected: string | null; onToggle: (id: string) => void; onDocument: (id: string) => void }) {
    return <div className="space-y-3">{chunks.map(chunk => <article key={chunk.id} id={`client-source-${chunk.id}`} tabIndex={-1} className="evidence-card" data-highlighted={selected === chunk.id}>
        <button className="w-full p-4 text-left" aria-expanded={expanded.includes(chunk.id)} aria-label={`Inspect ${chunk.filename}, chunk ${chunk.index}`} onClick={() => onToggle(chunk.id)}><div className="flex items-center gap-2"><span className="mono rounded bg-accent px-1.5 py-1 text-[10px] text-primary">{chunk.isNeighbor ? "+" : `#${chunk.rankAfter}`}</span><FileText size={13} className="text-muted-foreground" /><span className="min-w-0 truncate text-xs font-medium">{chunk.filename}</span><Badge variant="outline" className={cn("ml-auto text-[9px]", chunk.inContext && "text-[var(--success)]")}>{chunk.inContext ? <><CheckCheck size={10} />{chunk.isNeighbor ? "Neighbor" : "In context"}</> : "Excluded"}</Badge><ChevronDown size={12} /></div><p className={cn("mt-3 whitespace-pre-wrap text-[11px] leading-6 text-muted-foreground", !expanded.includes(chunk.id) && "line-clamp-2")}>{chunk.content}</p>{chunk.exclusionReason && <p className="mt-2 text-[10px] text-[var(--warning)]">{chunk.exclusionReason}</p>}</button>
        {expanded.includes(chunk.id) && <div className="border-t p-4"><dl className="grid grid-cols-2 gap-4 text-[10px] sm:grid-cols-4">{[["Normalized cosine", score(chunk.vectorScore)], ["Lexical score", score(chunk.lexicalScore)], ["RRF score", score(chunk.fusionScore)], ["Reranker score", score(chunk.rerankerScore)], ["Before → after", `${chunk.rankBefore} → ${chunk.rankAfter}`], ["Chunk index", chunk.index], ["Estimated tokens", Math.ceil(chunk.content.length / 4)]].map(([label, value]) => <div key={label}><dt className="text-muted-foreground">{label}</dt><dd className="mono mt-1">{value}</dd></div>)}</dl><div className="mt-4 flex justify-between gap-4"><span className="mono break-all text-[9px] text-muted-foreground">{chunk.id}</span><Button size="sm" variant="outline" className="shrink-0" onClick={() => onDocument(chunk.documentId)}>View document</Button></div></div>}
    </article>)}</div>;
}
