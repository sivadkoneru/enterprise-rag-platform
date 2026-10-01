"use client";

import {
    useEffect,
    useMemo,
    useRef,
    useState,
    useSyncExternalStore,
} from "react";
import {
    ArrowDown,
    ArrowRight,
    BookOpen,
    Check,
    CheckCheck,
    ChevronRight,
    CircleAlert,
    Copy,
    Database,
    FileSearch,
    FileText,
    History,
    Layers3,
    Loader2,
    Play,
    RotateCcw,
    ShieldCheck,
    Sparkles,
    Square,
    Terminal,
} from "lucide-react";
import type { CorpusId, QueryRun, TraceStage } from "@/lib/contracts";
import { DEFAULT_CONFIG, DEFAULT_QUESTION } from "@/lib/constants";
import { corpora, EXAMPLE_QUESTIONS } from "@/lib/demo/corpus";
import { gateway } from "@/lib/demo/gateway";
import {
    historySnapshot,
    readHistory,
    saveHistory,
    subscribeHistory,
} from "@/lib/demo/history";
import { Panel, PageFooter, PageHeading } from "@/components/shared/panel";
import { Hint } from "@/components/shared/hint";
import { Drawer } from "@/components/shared/drawer";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { CorpusDrawer, DocumentDrawer } from "@/features/corpus/corpus-drawer";
import { RetrievalSettings } from "./retrieval-settings";
import { PipelineTrace } from "./pipeline-trace";
import { EvidenceList } from "./evidence-list";

const subscribeHydration = () => () => {};

export function PlaygroundPage() {
    const hydrated = useSyncExternalStore(
        subscribeHydration,
        () => true,
        () => false,
    );
    const [corpusId, setCorpusId] = useState<CorpusId>("handbook");
    const [question, setQuestion] = useState(DEFAULT_QUESTION);
    const [config, setConfig] = useState({ ...DEFAULT_CONFIG });
    const [run, setRun] = useState<QueryRun | null>(null);
    const [stages, setStages] = useState<TraceStage[]>([]);
    const [running, setRunning] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [corpusOpen, setCorpusOpen] = useState(false);
    const [historyOpen, setHistoryOpen] = useState(false);
    const [documentId, setDocumentId] = useState<string | null>(null);
    const [expandedIds, setExpandedIds] = useState<string[]>([]);
    const [highlightedId, setHighlightedId] = useState<string | null>(null);
    const [copied, setCopied] = useState(false);
    const [simulateFailure, setSimulateFailure] = useState(false);
    const abortRef = useRef<AbortController | null>(null);
    const copyTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const snapshot = useSyncExternalStore(
        subscribeHistory,
        historySnapshot,
        () => "",
    );
    const history = useMemo(() => readHistory(snapshot), [snapshot]);
    const corpus = corpora.find((item) => item.id === corpusId)!;
    useEffect(
        () => () => {
            abortRef.current?.abort();
            if (copyTimer.current) clearTimeout(copyTimer.current);
        },
        [],
    );

    async function execute() {
        abortRef.current?.abort();
        const controller = new AbortController();
        abortRef.current = controller;
        setRunning(true);
        setError("");
        setNotice("");
        setRun(null);
        setCopied(false);
        setStages([]);
        try {
            const result = await gateway.runQuery(
                { corpusId, question, config: { ...config }, simulateFailure },
                {
                    signal: controller.signal,
                    onEvent: (event) => {
                        if (abortRef.current === controller)
                            setStages(event.stages);
                    },
                },
            );
            if (controller.signal.aborted || abortRef.current !== controller)
                return;
            setRun(result);
            setExpandedIds([]);
            setHighlightedId(null);
            if (!saveHistory(result))
                setNotice(
                    "This browser could not save session history. Your current result is still available.",
                );
        } catch (err) {
            if (abortRef.current !== controller) return;
            if (err instanceof DOMException && err.name === "AbortError") {
                setNotice(
                    "Query canceled. Adjust your settings and run again.",
                );
                setStages([]);
            } else {
                setError(
                    err instanceof Error
                        ? err.message
                        : "The demo query could not complete. Please retry.",
                );
                setSimulateFailure(false);
            }
        } finally {
            if (abortRef.current === controller) setRunning(false);
        }
    }
    function restore(item: QueryRun) {
        abortRef.current?.abort();
        abortRef.current = null;
        setRunning(false);
        setError("");
        setNotice("Restored a query and its settings from this session.");
        setCorpusId(item.request.corpusId);
        setQuestion(item.request.question);
        setConfig({ ...item.request.config });
        setRun(item);
        setStages(item.trace);
        setExpandedIds([]);
        setHighlightedId(null);
        setHistoryOpen(false);
    }
    function revealSource(number: number) {
        const citation = run?.citations.find((item) => item.number === number);
        if (!citation) return;
        setExpandedIds((current) => [
            ...new Set([...current, citation.chunkId]),
        ]);
        setHighlightedId(citation.chunkId);
        requestAnimationFrame(() => {
            const element = document.getElementById(
                `source-${citation.chunkId}`,
            );
            element?.scrollIntoView({
                behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
                    .matches
                    ? "instant"
                    : "smooth",
                block: "center",
            });
            element?.focus({ preventScroll: true });
        });
    }
    async function copyAnswer() {
        if (!run) return;
        const text =
            run.answer
                .map(
                    (segment) =>
                        `${segment.text}${segment.citationNumber ? ` [${segment.citationNumber}]` : ""}`,
                )
                .join("\n\n") +
            "\n\n" +
            run.citations
                .map((citation) => {
                    const chunk = run.context.find(
                        (item) => item.id === citation.chunkId,
                    )!;
                    return `[${citation.number}] ${chunk.filename} — ${chunk.section} (chunk ${chunk.index})`;
                })
                .join("\n");
        try {
            await navigator.clipboard.writeText(text);
            setCopied(true);
            if (copyTimer.current) clearTimeout(copyTimer.current);
            copyTimer.current = setTimeout(() => setCopied(false), 2000);
        } catch {
            setNotice(
                "Clipboard access is unavailable. Select the answer text to copy it.",
            );
        }
    }
    const evidence = run
        ? [
              ...run.candidates,
              ...run.context.filter((chunk) => chunk.isNeighbor),
          ]
        : [];
    return (
        <>
            <PageHeading
                eyebrow="EXPERIMENT WORKSPACE"
                title="Playground"
                description="Ask a question. Inspect the evidence. Understand every step."
                action={
                    <Button
                        variant="outline"
                        size="sm"
                        className="gap-2 bg-card text-xs"
                        onClick={() => setHistoryOpen(true)}
                    >
                        <History size={14} />
                        Query history
                        {history.length > 0 && (
                            <span className="mono ml-1 rounded bg-muted px-1.5 text-[10px]">
                                {history.length}
                            </span>
                        )}
                    </Button>
                }
            />
            <div className="playground-grid">
                <div className="space-y-5">
                    <Panel
                        title="Query workspace"
                        icon={
                            <Terminal
                                size={14}
                                className="text-muted-foreground"
                            />
                        }
                        action={
                            <span className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
                                <span className="status-dot" />
                                Ready
                            </span>
                        }
                    >
                        <form
                            onSubmit={(event) => {
                                event.preventDefault();
                                void execute();
                            }}
                        >
                            <label className="field-label" htmlFor="corpus">
                                Document Corpus
                            </label>
                            <div className="relative">
                                <Database
                                    size={14}
                                    className="absolute left-3 top-3 text-muted-foreground"
                                />
                                <select
                                    id="corpus"
                                    className="field !pl-9"
                                    value={corpusId}
                                    disabled={!hydrated || running}
                                    onChange={(event) => {
                                        const value = event.target
                                            .value as CorpusId;
                                        setCorpusId(value);
                                        setQuestion(
                                            EXAMPLE_QUESTIONS[value][0],
                                        );
                                    }}
                                >
                                    {corpora.map((item) => (
                                        <option key={item.id} value={item.id}>
                                            {item.name}
                                        </option>
                                    ))}
                                </select>
                            </div>
                            <div className="mt-2.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-[10px] text-muted-foreground">
                                <span>{corpus.documentCount} documents</span>
                                <span className="h-1 w-1 rounded-full bg-border" />
                                <span>
                                    {corpus.chunkCount.toLocaleString()} chunks
                                </span>
                            </div>
                            <div className="mt-2 flex items-center justify-between">
                                <span className="text-[9px] text-muted-foreground">
                                    Last indexed: Demo dataset
                                </span>
                                <button
                                    type="button"
                                    onClick={() => setCorpusOpen(true)}
                                    className="flex items-center gap-1 text-[10px] font-medium text-primary"
                                >
                                    Browse Corpus
                                    <ArrowRight size={11} />
                                </button>
                            </div>
                            <div className="my-5 border-t" />
                            <label className="field-label" htmlFor="question">
                                Ask a question{" "}
                                <span className="flex items-center gap-1 text-[9px] font-normal text-muted-foreground">
                                    <ShieldCheck size={11} />
                                    Local demo
                                </span>
                            </label>
                            <textarea
                                id="question"
                                rows={4}
                                maxLength={2000}
                                disabled={!hydrated || running}
                                value={question}
                                onChange={(event) =>
                                    setQuestion(event.target.value)
                                }
                                className="field min-h-[122px] resize-y !text-xs !leading-6"
                                placeholder="What would you like to find in your documents?"
                                onKeyDown={(event) => {
                                    if (
                                        (event.metaKey || event.ctrlKey) &&
                                        event.key === "Enter" &&
                                        !running &&
                                        question.trim()
                                    ) {
                                        event.preventDefault();
                                        void execute();
                                    }
                                }}
                            />
                            <div className="mt-3 flex gap-2">
                                {running ? (
                                    <Button
                                        type="button"
                                        className="flex-1 text-xs"
                                        variant="outline"
                                        onClick={(event) => {
                                            event.preventDefault();
                                            abortRef.current?.abort();
                                        }}
                                    >
                                        <Square size={12} />
                                        Cancel query
                                    </Button>
                                ) : (
                                    <Button
                                        type="submit"
                                        className="flex-1 gap-2 text-xs shadow-sm"
                                        disabled={!hydrated || !question.trim()}
                                    >
                                        <Play size={12} fill="currentColor" />
                                        Run Query
                                        <span className="ml-auto rounded bg-white/15 px-1 py-0.5 text-[9px] opacity-75">
                                            ⌘ ↵
                                        </span>
                                    </Button>
                                )}
                                <Button
                                    type="button"
                                    variant="outline"
                                    className="text-xs"
                                    disabled={!hydrated || running}
                                    onClick={() => {
                                        setQuestion("");
                                        setRun(null);
                                        setStages([]);
                                        setError("");
                                        setNotice("");
                                    }}
                                >
                                    Clear
                                </Button>
                            </div>
                        </form>
                        <div className="mt-5">
                            <p className="eyebrow mb-2 !text-[9px]">
                                Or try an example
                            </p>
                            <div className="space-y-1">
                                {EXAMPLE_QUESTIONS[corpusId]
                                    .slice(0, 3)
                                    .map((example) => (
                                        <button
                                            key={example}
                                            disabled={!hydrated || running}
                                            onClick={() => setQuestion(example)}
                                            className="group flex w-full items-start gap-2 rounded-md px-1 py-1.5 text-left text-[10px] leading-5 text-muted-foreground hover:bg-muted hover:text-primary"
                                        >
                                            <ChevronRight
                                                size={11}
                                                className="mt-1 shrink-0"
                                            />
                                            {example}
                                        </button>
                                    ))}
                            </div>
                        </div>
                    </Panel>
                    <RetrievalSettings
                        config={config}
                        onChange={setConfig}
                        disabled={!hydrated || running}
                    />
                    <details className="px-1 text-[10px] text-muted-foreground">
                        <summary className="list-none">
                            Demo controls <span className="ml-1">⌄</span>
                        </summary>
                        <label className="mt-3 flex items-center gap-2">
                            <input
                                type="checkbox"
                                checked={simulateFailure}
                                disabled={!hydrated || running}
                                onChange={(event) =>
                                    setSimulateFailure(event.target.checked)
                                }
                                className="accent-primary"
                            />
                            Simulate a recoverable provider timeout
                        </label>
                        <p className="mt-2 leading-5">
                            A single failed run demonstrates the error and retry
                            path.
                        </p>
                    </details>
                </div>
                <div className="min-w-0 space-y-5" aria-busy={running}>
                    {error && (
                        <div
                            className="flex items-start gap-3 rounded-lg border border-destructive/30 bg-destructive/5 p-4"
                            role="alert"
                        >
                            <CircleAlert
                                size={17}
                                className="mt-0.5 shrink-0 text-destructive"
                            />
                            <div className="flex-1">
                                <p className="text-xs font-medium">
                                    Query could not complete
                                </p>
                                <p className="mt-1 text-[11px] leading-5 text-muted-foreground">
                                    {error}
                                </p>
                                <Button
                                    variant="outline"
                                    size="sm"
                                    className="mt-3 text-xs"
                                    onClick={() => void execute()}
                                >
                                    <RotateCcw size={12} />
                                    Retry query
                                </Button>
                            </div>
                        </div>
                    )}
                    {notice && (
                        <p
                            className="rounded-md border bg-card px-4 py-3 text-[11px] text-muted-foreground"
                            role="status"
                        >
                            {notice}
                        </p>
                    )}
                    <Panel
                        title="Grounded Answer"
                        icon={<Sparkles size={15} className="text-primary" />}
                        action={
                            <Badge
                                variant="outline"
                                className="gap-1.5 rounded text-[9px] font-normal"
                            >
                                {run ? (
                                    <>
                                        <CheckCheck size={11} />
                                        Evidence linked
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
                                    <Loader2
                                        size={15}
                                        className="animate-spin"
                                    />
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
                                            : "Answer grounded in selected context"}
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
                                            Confidence:{" "}
                                            <strong className="font-medium text-foreground">
                                                {run.confidence}
                                            </strong>
                                            <Hint text={run.confidenceReason} />
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
                                                {run.contextTokens +
                                                    run.outputTokens}
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
                                        {copied ? (
                                            <Check size={11} />
                                        ) : (
                                            <Copy size={11} />
                                        )}
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
                                    Run a query to see a grounded answer,
                                    inspect its sources, and follow the pipeline
                                    from retrieval to response.
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
                    {run ? (
                        <section>
                            <div className="mb-3 flex items-center justify-between">
                                <h2 className="panel-title">
                                    <BookOpen
                                        size={15}
                                        className="text-primary"
                                    />
                                    Retrieved Context
                                    <span className="mono ml-1 rounded border bg-card px-1.5 py-0.5 text-[10px] text-muted-foreground">
                                        {evidence.length}
                                    </span>
                                </h2>
                                <span className="text-[10px] text-muted-foreground">
                                    {run.context.length} in context · click to
                                    inspect
                                </span>
                            </div>
                            <EvidenceList
                                chunks={evidence}
                                expandedIds={expandedIds}
                                onToggle={(id) =>
                                    setExpandedIds((ids) =>
                                        ids.includes(id)
                                            ? ids.filter((item) => item !== id)
                                            : [...ids, id],
                                    )
                                }
                                highlightedId={highlightedId}
                                onDocument={setDocumentId}
                            />
                        </section>
                    ) : (
                        !running && (
                            <div className="grid grid-cols-3 gap-3">
                                {[
                                    {
                                        icon: Database,
                                        title: "Retrieve",
                                        text: "Find relevant passages",
                                    },
                                    {
                                        icon: Layers3,
                                        title: "Ground",
                                        text: "Build bounded context",
                                    },
                                    {
                                        icon: CheckCheck,
                                        title: "Validate",
                                        text: "Link claims to sources",
                                    },
                                ].map(({ icon: Icon, title, text }, index) => (
                                    <div
                                        className="panel relative p-4"
                                        key={title}
                                    >
                                        <div className="mb-3 flex items-center justify-between">
                                            <Icon
                                                size={17}
                                                strokeWidth={1.5}
                                                className="text-primary"
                                            />
                                            <span className="mono text-[9px] text-muted-foreground">
                                                0{index + 1}
                                            </span>
                                        </div>
                                        <h3 className="text-xs font-medium">
                                            {title}
                                        </h3>
                                        <p className="mt-1.5 text-[10px] leading-5 text-muted-foreground">
                                            {text}
                                        </p>
                                    </div>
                                ))}
                            </div>
                        )
                    )}
                    <PipelineTrace stages={stages} running={running} />
                    {!run && !running && (
                        <p className="flex items-center justify-center gap-1.5 text-[10px] text-muted-foreground">
                            <ArrowDown size={11} />
                            Your query, evidence, and trace stay in this browser
                            session.
                        </p>
                    )}
                </div>
            </div>
            <CorpusDrawer
                corpusId={corpusId}
                open={corpusOpen}
                onOpenChange={setCorpusOpen}
            />
            <DocumentDrawer
                documentId={documentId}
                onClose={() => setDocumentId(null)}
            />
            <Drawer
                open={historyOpen}
                onOpenChange={setHistoryOpen}
                title="Query history"
                description="The last 20 successful runs in this browser session. Restore a run with its original settings."
            >
                <div className="space-y-3 pt-5">
                    {history.length === 0 ? (
                        <div className="rounded-lg border border-dashed py-12 text-center">
                            <History
                                size={24}
                                className="mx-auto mb-3 text-muted-foreground"
                            />
                            <p className="text-xs text-muted-foreground">
                                Your first query will appear here.
                            </p>
                        </div>
                    ) : (
                        history.map((item) => (
                            <button
                                key={item.id}
                                className="block w-full rounded-lg border p-4 text-left hover:border-primary/50 hover:bg-accent/20"
                                onClick={() => restore(item)}
                            >
                                <div className="mb-2 flex justify-between text-[10px] text-muted-foreground">
                                    <span>
                                        {
                                            corpora.find(
                                                (corpus) =>
                                                    corpus.id ===
                                                    item.request.corpusId,
                                            )?.name
                                        }
                                    </span>
                                    <span>
                                        {new Date(
                                            item.createdAt,
                                        ).toLocaleTimeString([], {
                                            hour: "2-digit",
                                            minute: "2-digit",
                                        })}
                                    </span>
                                </div>
                                <p className="text-xs font-medium leading-6">
                                    {item.request.question}
                                </p>
                                <p className="mono mt-2 text-[10px] text-muted-foreground">
                                    {item.request.config.strategy} · top{" "}
                                    {item.request.config.topK} ·{" "}
                                    {item.totalLatencyMs} ms
                                </p>
                            </button>
                        ))
                    )}
                </div>
            </Drawer>
            <PageFooter />
        </>
    );
}
