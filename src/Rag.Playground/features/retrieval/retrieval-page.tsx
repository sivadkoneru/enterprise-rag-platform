"use client";
import { useMemo, useState } from "react";
import {
    ArrowRight,
    ArrowUpRight,
    Database,
    FileSearch,
    FileText,
    Search,
    SlidersHorizontal,
    Target,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Panel, PageFooter, PageHeading } from "@/components/shared/panel";
import { RetrievalScoreChart } from "./score-chart";
import { RetrievalSettings } from "@/features/playground/retrieval-settings";
import { DocumentDrawer } from "@/features/corpus/corpus-drawer";
import { DEFAULT_CONFIG, DEFAULT_QUESTION } from "@/lib/constants";
import { corpora, EXAMPLE_QUESTIONS } from "@/lib/demo/corpus";
import { retrieve } from "@/lib/demo/gateway";
import type { CorpusId } from "@/lib/contracts";
import { cn } from "@/lib/utils";

import { Highlight } from "./lexical-highlight";

export function RetrievalPage() {
    const [corpusId, setCorpusId] = useState<CorpusId>("handbook");
    const [question, setQuestion] = useState(DEFAULT_QUESTION);
    const [config, setConfig] = useState({ ...DEFAULT_CONFIG });
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [documentId, setDocumentId] = useState<string | null>(null);
    const result = useMemo(() => {
        try {
            return { ...retrieve({ corpusId, question, config }), error: "" };
        } catch (error) {
            return {
                candidates: [],
                context: [],
                supported: false,
                error:
                    error instanceof Error ? error.message : "Invalid settings",
            };
        }
    }, [corpusId, question, config]);
    const selected =
        result.candidates.find((chunk) => chunk.id === selectedId) ??
        result.candidates[0];
    return (
        <>
            <PageHeading
                eyebrow="RETRIEVAL INTELLIGENCE"
                title="Follow the signal"
                description="Inspect candidate scores, ranking changes, and the evidence that makes the cut."
                action={
                    <Badge variant="outline" className="gap-1.5 py-1.5">
                        <SlidersHorizontal size={12} />
                        Interactive demo profiles
                    </Badge>
                }
            />
            <Panel bodyClassName="grid gap-4 md:grid-cols-[1fr_250px]">
                <div>
                    <label className="field-label" htmlFor="retrieval-query">
                        Query
                    </label>
                    <div className="relative">
                        <Search
                            size={14}
                            className="absolute left-3 top-3 text-muted-foreground"
                        />
                        <input
                            id="retrieval-query"
                            className="field !pl-9"
                            value={question}
                            onChange={(event) =>
                                setQuestion(event.target.value)
                            }
                            maxLength={2000}
                        />
                    </div>
                </div>
                <div>
                    <label className="field-label" htmlFor="retrieval-corpus">
                        Document Corpus
                    </label>
                    <select
                        id="retrieval-corpus"
                        className="field"
                        value={corpusId}
                        onChange={(event) => {
                            const id = event.target.value as CorpusId;
                            setCorpusId(id);
                            setQuestion(EXAMPLE_QUESTIONS[id][0]);
                        }}
                    >
                        {corpora.map((corpus) => (
                            <option key={corpus.id} value={corpus.id}>
                                {corpus.name}
                            </option>
                        ))}
                    </select>
                </div>
            </Panel>
            <div className="mt-5 grid items-start gap-5 xl:grid-cols-[320px_minmax(0,1fr)]">
                <div className="space-y-5">
                    <RetrievalSettings config={config} onChange={setConfig} />
                    <div className="rounded-lg border bg-accent/25 p-4">
                        <div className="mb-2 flex items-center gap-2 text-xs font-medium">
                            <Target size={14} className="text-primary" />A
                            threshold is a trade-off
                        </div>
                        <p className="text-[11px] leading-6 text-muted-foreground">
                            Higher thresholds can remove noisy passages, but
                            also exclude useful evidence. Compare the score
                            distribution with the actual source text.
                        </p>
                        <div className="mt-4 flex justify-between border-t pt-3 text-[10px]">
                            <span className="text-muted-foreground">
                                Admitted to context
                            </span>
                            <span className="mono">
                                {result.context.length} /{" "}
                                {result.candidates.length} candidates
                                {config.neighbors ? " + neighbors" : ""}
                            </span>
                        </div>
                    </div>
                </div>
                <div className="min-w-0 space-y-5">
                    {result.error ? (
                        <div
                            role="alert"
                            className="panel border-destructive/30 p-5 text-xs text-destructive"
                        >
                            {result.error}
                        </div>
                    ) : (
                        <>
                            <RetrievalScoreChart
                                candidates={result.candidates}
                                config={config}
                                selectedId={selected?.id}
                                onSelect={setSelectedId}
                            />
                            {selected && (
                                <Panel
                                    title="Retrieval Inspector"
                                    icon={
                                        <FileSearch
                                            size={15}
                                            className="text-primary"
                                        />
                                    }
                                    action={
                                        <Badge
                                            variant="outline"
                                            className={cn(
                                                "text-[9px]",
                                                selected.inContext &&
                                                    "border-transparent bg-[var(--success-bg)] text-[var(--success)]",
                                            )}
                                        >
                                            {selected.inContext
                                                ? "Included in context"
                                                : "Excluded from context"}
                                        </Badge>
                                    }
                                >
                                    <div
                                        className="mb-5 flex flex-wrap gap-1.5"
                                        role="group"
                                        aria-label="Select retrieved chunk"
                                    >
                                        {result.candidates.map((chunk) => (
                                            <button
                                                key={chunk.id}
                                                onClick={() =>
                                                    setSelectedId(chunk.id)
                                                }
                                                aria-pressed={
                                                    selected.id === chunk.id
                                                }
                                                className={cn(
                                                    "mono rounded-md border px-2.5 py-1.5 text-[10px]",
                                                    selected.id === chunk.id
                                                        ? "border-primary/40 bg-accent text-primary"
                                                        : "text-muted-foreground hover:bg-muted",
                                                )}
                                            >
                                                #{chunk.rankAfter} ·{" "}
                                                {chunk.index}
                                            </button>
                                        ))}
                                    </div>
                                    <div className="grid gap-4 md:grid-cols-[.8fr_1.2fr]">
                                        <div className="rounded-lg border bg-muted/30 p-4">
                                            <div className="eyebrow mb-3 flex items-center gap-2">
                                                <Search size={12} />
                                                Query
                                            </div>
                                            <p className="text-xs leading-7">
                                                <Highlight
                                                    text={question}
                                                    concepts={selected.concepts}
                                                />
                                            </p>
                                            <div className="mt-5 border-t pt-3">
                                                <span className="text-[10px] text-muted-foreground">
                                                    Relevant concepts
                                                </span>
                                                <div className="mt-2 flex flex-wrap gap-1.5">
                                                    {selected.concepts.map(
                                                        (concept) => (
                                                            <Badge
                                                                key={concept}
                                                                variant="secondary"
                                                                className="text-[9px] font-normal"
                                                            >
                                                                {concept}
                                                            </Badge>
                                                        ),
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                        <div className="rounded-lg border p-4">
                                            <div className="eyebrow mb-3 flex items-center gap-2">
                                                <FileText size={12} />
                                                Retrieved Chunk
                                            </div>
                                            <h3 className="mb-2 text-xs font-medium">
                                                {selected.section}
                                            </h3>
                                            <p className="text-[11px] leading-7 text-muted-foreground">
                                                <Highlight
                                                    text={selected.content}
                                                    concepts={selected.concepts}
                                                />
                                            </p>
                                        </div>
                                    </div>
                                    <dl className="mt-5 grid grid-cols-2 gap-4 rounded-lg border bg-muted/20 p-4 sm:grid-cols-3">
                                        {[
                                            [
                                                "Vector similarity",
                                                selected.vectorScore.toFixed(3),
                                            ],
                                            [
                                                "Illustrative reranker score",
                                                selected.rerankerScore?.toFixed(
                                                    3,
                                                ) ?? "Disabled",
                                            ],
                                            [
                                                "Retrieval score",
                                                selected.retrievalScore.toFixed(
                                                    3,
                                                ),
                                            ],
                                            ["Document", selected.filename],
                                            ["Section", selected.section],
                                            [
                                                "Rank before → after",
                                                `${selected.rankBefore} → ${selected.rankAfter}`,
                                            ],
                                        ].map(([label, value]) => (
                                            <div key={label}>
                                                <dt className="text-[10px] text-muted-foreground">
                                                    {label}
                                                </dt>
                                                <dd className="mono mt-1.5 break-words text-[11px]">
                                                    {value}
                                                </dd>
                                            </div>
                                        ))}
                                    </dl>
                                    {selected.exclusionReason && (
                                        <p className="mt-3 text-[11px] text-[var(--warning)]">
                                            Excluded: {selected.exclusionReason}
                                            .
                                        </p>
                                    )}
                                    <div className="mt-4 flex items-center justify-between">
                                        <span className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
                                            <Database size={11} />
                                            {selected.tokens} estimated tokens
                                        </span>
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            className="text-[10px]"
                                            onClick={() =>
                                                setDocumentId(
                                                    selected.documentId,
                                                )
                                            }
                                        >
                                            View document
                                            <ArrowUpRight size={11} />
                                        </Button>
                                    </div>
                                </Panel>
                            )}
                            {!result.supported && (
                                <p className="flex items-center gap-2 rounded-lg border border-dashed p-4 text-xs text-muted-foreground">
                                    <ArrowRight size={14} />
                                    No supporting evidence qualifies for
                                    context. Try a different query or threshold.
                                </p>
                            )}
                        </>
                    )}
                </div>
            </div>
            <DocumentDrawer
                documentId={documentId}
                onClose={() => setDocumentId(null)}
            />
            <PageFooter />
        </>
    );
}
