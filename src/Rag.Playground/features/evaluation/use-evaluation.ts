"use client";
import { useMemo, useState } from "react";
import { caseStatus, getEvaluationDataset } from "@/lib/evaluation/data";
import type { EvaluationCase, StrategyBenchmark } from "@/lib/contracts";

export const dataset = getEvaluationDataset();
export const statuses = [
    "Pass",
    "Partial coverage",
    "Retrieval miss",
    "Citation mismatch",
    "False support",
    "Harness rejected unsupported case",
];
export function outcomeFor(strategy: StrategyBenchmark, id: string) {
    return strategy.outcomes.find((outcome) => outcome.questionId === id);
}
export function useEvaluation() {
    const [strategyId, setStrategyId] = useState("recursive");
    const [search, setSearch] = useState("");
    const [difficulty, setDifficulty] = useState("all");
    const [type, setType] = useState("all");
    const [status, setStatus] = useState("all");
    const [selectedCase, setSelectedCase] = useState<EvaluationCase | null>(
        null,
    );
    const [resource, setResource] = useState<
        "indexEmbedCalls" | "averageContextTokens"
    >("indexEmbedCalls");
    const [chartScope, setChartScope] = useState("all");
    const [topK, setTopK] = useState(10);
    const [page, setPage] = useState(1);
    const pageSize = 10;
    function changeStrategy(value: string) {
        setStrategyId(value);
        setPage(1);
    }
    const strategy =
        dataset.strategies.find((s) => s.strategy === strategyId) ??
        dataset.strategies[0];
    const chartStrategies =
        chartScope === "selected" ? [strategy] : dataset.strategies;
    const filteredCases = useMemo(
        () =>
            dataset.questions.filter((question) => {
                const outcome = outcomeFor(strategy, question.id);
                return (
                    `${question.id} ${question.question} ${question.goldAnchors.map((a) => a.section).join(" ")}`
                        .toLowerCase()
                        .includes(search.toLowerCase()) &&
                    (difficulty === "all" ||
                        question.difficulty === difficulty) &&
                    (type === "all" || question.type === type) &&
                    (status === "all" ||
                        (outcome &&
                            caseStatus(
                                outcome,
                                question.goldAnchors.length > 0,
                            ) === status))
                );
            }),
        [strategy, search, difficulty, type, status],
    );
    const pageCount = Math.max(1, Math.ceil(filteredCases.length / pageSize));
    const currentPage = Math.min(page, pageCount);
    const pageStart = (currentPage - 1) * pageSize;
    const visibleCases = filteredCases.slice(pageStart, pageStart + pageSize);
    const selectedOutcome = selectedCase
        ? outcomeFor(strategy, selectedCase.id)
        : undefined;

    return {
        strategyId,
        search,
        setSearch,
        difficulty,
        setDifficulty,
        type,
        setType,
        status,
        setStatus,
        selectedCase,
        setSelectedCase,
        resource,
        setResource,
        chartScope,
        setChartScope,
        topK,
        setTopK,
        setPage,
        pageSize,
        changeStrategy,
        strategy,
        chartStrategies,
        filteredCases,
        pageCount,
        currentPage,
        pageStart,
        visibleCases,
        selectedOutcome,
    };
}
