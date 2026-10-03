"use client";
import {
    useEffect,
    useMemo,
    useRef,
    useState,
    useSyncExternalStore,
} from "react";
import type { CorpusId, QueryRun, TraceStage } from "@/lib/contracts";
import { DEFAULT_CONFIG, DEFAULT_QUESTION } from "@/lib/constants";
import { corpora } from "@/lib/demo/corpus";
import { gateway } from "@/lib/demo/gateway";
import {
    historySnapshot,
    readHistory,
    saveHistory,
    subscribeHistory,
} from "@/lib/demo/history";
const subscribeHydration = () => () => {};
export function usePlayground() {
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
            "Demo simulation — prepared evidence, no external model or search calls.\n\n" +
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
    return {
        hydrated,
        corpusId,
        setCorpusId,
        question,
        setQuestion,
        config,
        setConfig,
        run,
        setRun,
        stages,
        setStages,
        running,
        setRunning,
        error,
        setError,
        notice,
        setNotice,
        corpusOpen,
        setCorpusOpen,
        historyOpen,
        setHistoryOpen,
        documentId,
        setDocumentId,
        expandedIds,
        setExpandedIds,
        highlightedId,
        setHighlightedId,
        copied,
        simulateFailure,
        setSimulateFailure,
        abortRef,
        history,
        corpus,
        execute,
        restore,
        revealSource,
        copyAnswer,
        evidence,
    };
}
