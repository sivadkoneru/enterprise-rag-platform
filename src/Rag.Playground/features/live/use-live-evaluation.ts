"use client";
import type { ClientJob, EvaluationRun } from "@/lib/live/contracts";
import { parseEvaluationQuestions } from "@/lib/live/evaluation";
import { liveGateway } from "@/lib/live/gateway";
import { useState } from "react";
import { initialQuerySettings } from "./query-controls";
import { useClientData } from "./use-client-data";

import { type Outcome } from "./evaluation-metrics";
export function useLiveEvaluation() {
    const data = useClientData();
    const [customSettings, setSettings] = useState<
        typeof initialQuerySettings | null
    >(null);
    const settings =
        customSettings ??
        data.capabilities?.defaultQuery ??
        initialQuerySettings;
    const [difficulty, setDifficulty] = useState("all");
    const [dataset, setDataset] = useState("");
    const [selectedProfiles, setSelectedProfiles] = useState<string[]>([]);
    const [job, setJob] = useState<ClientJob | null>(null);
    const [report, setReport] = useState<EvaluationRun | null>(null);
    const [reports, setReports] = useState<EvaluationRun[]>([]);
    const [activeProfile, setActiveProfile] = useState("");
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const [search, setSearch] = useState("");
    const [resultFilter, setResultFilter] = useState("all");
    const [selected, setSelected] = useState<Outcome | null>(null);
    const [expanded, setExpanded] = useState<string[]>([]);
    const [document, setDocument] = useState<{
        profile: string;
        id: string;
    } | null>(null);
    const [resource, setResource] = useState<
        "embeddingOperations" | "averageContextTokens"
    >("embeddingOperations");
    const [page, setPage] = useState(0);
    const resultProfile =
        report?.profiles.find((p) => p.profileId === activeProfile) ??
        report?.profiles[0];
    const eligibleIds = selectedProfiles.filter((id) =>
        data.profiles.some((p) => p.id === id && p.status === "ready"),
    );
    async function run() {
        setBusy(true);
        setError("");
        try {
            const questions = parseEvaluationQuestions(dataset);
            setJob(
                await liveGateway.evaluate({
                    questions,
                    profileIds: eligibleIds,
                    ...settings,
                    topK: 5,
                    reranker:
                        settings.reranker && !!data.capabilities?.reranker,
                }),
            );
            setReport(null);
        } catch (err) {
            setError(
                err instanceof Error
                    ? err.message
                    : "Evaluation could not start.",
            );
        } finally {
            setBusy(false);
        }
    }
    async function completed(next: ClientJob) {
        if (!["complete", "completed"].includes(next.status)) return;
        try {
            const result = await liveGateway.getEvaluation(next.id);
            setReport(result);
            setReports((current) => [
                result,
                ...current.filter((r) => r.id !== result.id),
            ]);
            setActiveProfile(result.profiles[0]?.profileId ?? "");
        } catch (err) {
            setError(
                err instanceof Error
                    ? err.message
                    : "Evaluation report unavailable.",
            );
        }
    }
    async function importFile(file?: File) {
        if (!file) return;
        if (file.size > 2_000_000) {
            setError("Evaluation files must be smaller than 2 MB.");
            return;
        }
        try {
            const text = await file.text();
            parseEvaluationQuestions(text);
            setDataset(text);
            setError("");
        } catch (err) {
            setError(
                err instanceof Error ? err.message : "Invalid evaluation file.",
            );
        }
    }
    function status(outcome: Outcome) {
        return outcome.expectedAbstention
            ? outcome.run.abstained
                ? "Abstained correctly"
                : "Unexpected answer"
            : outcome.metrics.recallAt5 !== null &&
                outcome.metrics.recallAt5 < 1
              ? "Retrieval miss"
              : outcome.metrics.citationAccuracy === 0
                ? "Citation mismatch"
                : "Pass";
    }
    const cases = (resultProfile?.outcomes ?? []).filter(
        (item) =>
            item.question.toLowerCase().includes(search.toLowerCase()) &&
            (difficulty === "all" ||
                report?.questions?.find((q) => q.id === item.questionId)
                    ?.difficulty === difficulty) &&
            (resultFilter === "all" || status(item) === resultFilter),
    );
    const chartData =
        report?.profiles.map((p) => ({
            ...p,
            recall:
                p.metrics.recallAt5 == null ? null : p.metrics.recallAt5 * 100,
        })) ?? [];
    return {
        customSettings,
        setSettings,
        difficulty,
        setDifficulty,
        dataset,
        setDataset,
        selectedProfiles,
        setSelectedProfiles,
        job,
        setJob,
        report,
        setReport,
        reports,
        setReports,
        activeProfile,
        setActiveProfile,
        error,
        setError,
        busy,
        setBusy,
        search,
        setSearch,
        resultFilter,
        setResultFilter,
        selected,
        setSelected,
        expanded,
        setExpanded,
        document,
        setDocument,
        resource,
        setResource,
        page,
        setPage,
        data,
        settings,
        resultProfile,
        eligibleIds,
        cases,
        chartData,
        run,
        completed,
        importFile,
        status,
    };
}
