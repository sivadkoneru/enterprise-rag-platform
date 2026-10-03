"use client";
import type { TraceStage } from "@/lib/contracts";
import type { LiveQueryRequest, LiveQueryRun } from "@/lib/live/contracts";
import { liveGateway } from "@/lib/live/gateway";
import { useEffect, useRef, useState } from "react";
import { initialQuerySettings } from "./query-controls";
import { useClientData } from "./use-client-data";

export function useLiveQuery() {
    const data = useClientData();
    const [question, setQuestion] = useState("");
    const [customSettings, setSettings] = useState<
        typeof initialQuerySettings | null
    >(null);
    const settings =
        customSettings ??
        data.capabilities?.defaultQuery ??
        initialQuerySettings;
    const [run, setRun] = useState<LiveQueryRun | null>(null);
    const [request, setRequest] = useState<LiveQueryRequest | null>(null);
    const [trace, setTrace] = useState<TraceStage[]>([]);
    const [running, setRunning] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [history, setHistory] = useState<
        { run: LiveQueryRun; request: LiveQueryRequest }[]
    >([]);
    const [historyOpen, setHistoryOpen] = useState(false);
    const [expanded, setExpanded] = useState<string[]>([]);
    const [selected, setSelected] = useState<string | null>(null);
    const [document, setDocument] = useState<{
        profileId: string;
        id?: string;
    } | null>(null);
    const [copied, setCopied] = useState(false);
    const abort = useRef<AbortController | null>(null);
    useEffect(() => () => abort.current?.abort(), []);
    const profile = data.profiles.find((p) => p.id === data.profileId);

    async function execute() {
        if (profile?.status !== "ready") {
            setError("Select a ready indexing profile before querying.");
            return;
        }
        abort.current?.abort();
        const controller = new AbortController();
        abort.current = controller;
        const snapshot = {
            ...settings,
            question: question.trim(),
            corpusId: data.corpusId,
            profileId: data.profileId,
            reranker: settings.reranker && !!data.capabilities?.reranker,
        };
        setRunning(true);
        setError("");
        setNotice("");
        setRun(null);
        setTrace([]);
        setRequest(snapshot);
        setCopied(false);
        try {
            const result = await liveGateway.runQuery(snapshot, {
                signal: controller.signal,
                onStage(stage) {
                    if (abort.current === controller)
                        setTrace((current) =>
                            current.some((s) => s.id === stage.id)
                                ? current.map((s) =>
                                      s.id === stage.id ? stage : s,
                                  )
                                : [...current, stage],
                        );
                },
            });
            if (controller.signal.aborted || abort.current !== controller)
                return;
            setRun(result);
            setTrace(result.trace);
            setExpanded([]);
            setSelected(null);
            setHistory((current) =>
                [{ run: result, request: snapshot }, ...current].slice(0, 20),
            );
        } catch (err) {
            if (abort.current !== controller) return;
            if (controller.signal.aborted) {
                setNotice("Query canceled. No result has been saved.");
                setTrace((current) =>
                    current.map((s) =>
                        s.status === "running" || s.status === "queued"
                            ? { ...s, status: "canceled" }
                            : s,
                    ),
                );
            } else
                setError(err instanceof Error ? err.message : "Query failed.");
        } finally {
            if (abort.current === controller) setRunning(false);
        }
    }
    function reveal(id: string) {
        setSelected(id);
        setExpanded((current) => [...new Set([...current, id])]);
        requestAnimationFrame(() => {
            const element = window.document.getElementById(
                `client-source-${id}`,
            );
            element?.scrollIntoView({
                block: "center",
                behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
                    .matches
                    ? "instant"
                    : "smooth",
            });
            element?.focus({ preventScroll: true });
        });
    }
    async function copy() {
        try {
            await navigator.clipboard.writeText(run?.answer ?? "");
            setCopied(true);
        } catch {
            setNotice(
                "Clipboard access is unavailable. Select the answer text to copy it.",
            );
        }
    }
    const evidence = run
        ? [
              ...run.candidates,
              ...run.context.filter(
                  (c) =>
                      !run.candidates.some(
                          (candidate) => candidate.id === c.id,
                      ),
              ),
          ]
        : [];
    return {
        question,
        setQuestion,
        customSettings,
        setSettings,
        run,
        setRun,
        request,
        setRequest,
        trace,
        setTrace,
        running,
        setRunning,
        error,
        setError,
        notice,
        setNotice,
        history,
        setHistory,
        historyOpen,
        setHistoryOpen,
        expanded,
        setExpanded,
        selected,
        setSelected,
        document,
        setDocument,
        copied,
        setCopied,
        data,
        settings,
        abort,
        profile,
        evidence,
        execute,
        reveal,
        copy,
    };
}
