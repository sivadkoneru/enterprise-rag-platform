"use client";
import { useMemo, useState } from "react";
import {
    ArrowUpRight,
    FlaskConical,
    Search,
    SlidersHorizontal,
    Info,
    ChevronRight,
    Database,
    CheckCircle2,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Panel, PageFooter, PageHeading } from "@/components/shared/panel";
import { Drawer } from "@/components/shared/drawer";
import { Hint } from "@/components/shared/hint";
import { STRATEGY_COLORS, STRATEGY_LABELS } from "@/lib/constants";
import {
    caseStatus,
    EVALUATION_PROFILE,
    getEvaluationDataset,
    METRICS,
    percent,
} from "@/lib/evaluation/data";
import type {
    EvaluationCase,
    EvaluationOutcome,
    StrategyBenchmark,
} from "@/lib/contracts";
import {
    BenchmarkBars,
    LatencyChart,
    TradeoffScatter,
} from "./evaluation-charts";

const dataset = getEvaluationDataset();
const statuses = [
    "Pass",
    "Partial coverage",
    "Retrieval miss",
    "Citation mismatch",
    "False support",
    "Abstained correctly",
];
function outcomeFor(strategy: StrategyBenchmark, id: string) {
    return strategy.outcomes.find((outcome) => outcome.questionId === id);
}
function SelectField({
    label,
    value,
    onChange,
    options,
}: {
    label: string;
    value: string;
    onChange: (value: string) => void;
    options: { value: string; label: string }[];
}) {
    return (
        <label className="flex min-w-0 flex-col gap-1.5">
            <span className="field-label">{label}</span>
            <select
                aria-label={label}
                className="field text-xs"
                value={value}
                onChange={(event) => onChange(event.target.value)}
            >
                {options.map((option) => (
                    <option key={option.value} value={option.value}>
                        {option.label}
                    </option>
                ))}
            </select>
        </label>
    );
}

export function EvaluationPage() {
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

    return (
        <>
            <PageHeading
                eyebrow="MEASURE & COMPARE"
                title="RAG Evaluation Dashboard"
                description="Follow the evidence. Compare retrieval quality, indexing cost, and the cases behind every score."
                action={
                    <Badge variant="outline" className="gap-1.5 py-1.5">
                        <Database size={12} /> Published benchmark ·{" "}
                        {dataset.questionCount} cases
                    </Badge>
                }
            />
            <Panel bodyClassName="flex flex-wrap items-center justify-between gap-4 !py-3">
                <div className="flex flex-wrap items-center gap-4">
                    <div className="flex items-center gap-2 text-xs font-medium">
                        <FlaskConical size={16} className="text-primary" />
                        Deterministic evaluation
                    </div>
                    <span className="text-muted text-[11px]">
                        3 documents ·{" "}
                        {EVALUATION_PROFILE.embeddingDimensions.toLocaleString()}{" "}
                        dimensions · top K {EVALUATION_PROFILE.topK}
                    </span>
                    <span className="text-muted text-[11px]">
                        Chunk size {EVALUATION_PROFILE.chunkSize} · overlap{" "}
                        {EVALUATION_PROFILE.chunkOverlap}
                    </span>
                </div>
                <SelectField
                    label="Active strategy"
                    value={strategyId}
                    onChange={changeStrategy}
                    options={dataset.strategies.map((s) => ({
                        value: s.strategy,
                        label: STRATEGY_LABELS[s.strategy],
                    }))}
                />
            </Panel>

            <div className="my-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-7">
                {METRICS.map((metric) => (
                    <Panel key={metric.key} bodyClassName="!p-4">
                        <div className="text-muted flex items-center justify-between gap-1 text-[10px] font-medium">
                            {metric.label}
                            <Hint text={metric.definition} />
                        </div>
                        <div className="metric-value mt-3 text-[25px]">
                            {percent(Number(strategy[metric.key]))}
                        </div>
                        <div className="text-muted mt-1.5 text-[10px]">
                            {STRATEGY_LABELS[strategy.strategy]} · published
                        </div>
                    </Panel>
                ))}
            </div>
            <div className="mb-5 flex gap-2 rounded-lg border border-primary/15 bg-primary/5 px-4 py-3 text-[11px] leading-5">
                <Info size={15} className="mt-0.5 shrink-0 text-primary" />
                <p>
                    <strong>Read these scores in context.</strong> Groundedness
                    is 100% by construction: the deterministic client copies a
                    sentence from retrieved context. Abstention accuracy scores
                    the harness support policy (z ≥{" "}
                    {EVALUATION_PROFILE.supportZThreshold}); the product does
                    not currently abstain. These scores do not measure
                    production model answer quality.
                </p>
            </div>

            <Panel
                title="Strategy benchmark"
                icon={<SlidersHorizontal size={14} />}
                action={
                    <span className="text-muted text-[10px]">
                        Highlighted: {STRATEGY_LABELS[strategyId]}
                    </span>
                }
            >
                <BenchmarkBars
                    strategies={dataset.strategies}
                    selected={strategyId}
                />
                <div className="mt-5 overflow-x-auto">
                    <table className="data-table w-full whitespace-nowrap text-[11px]">
                        <caption className="sr-only">
                            Exact published strategy values. Click a strategy to
                            inspect its cases.
                        </caption>
                        <thead>
                            <tr>
                                <th scope="col">Strategy</th>
                                {[
                                    "Recall @1",
                                    "Recall @5",
                                    "MRR @5",
                                    "Citation acc.",
                                    "Citation prec.",
                                    "Groundedness",
                                    "Abstention acc.",
                                    "False support",
                                    "Embed calls",
                                    "Context tokens",
                                ].map((label) => (
                                    <th key={label} scope="col">
                                        {label}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {dataset.strategies.map((s) => (
                                <tr
                                    key={s.strategy}
                                    className={
                                        s.strategy === strategyId
                                            ? "bg-primary/5"
                                            : ""
                                    }
                                >
                                    <th scope="row">
                                        <button
                                            className="flex items-center gap-2 rounded py-1 text-left font-medium focus-visible:outline-2 focus-visible:outline-primary"
                                            onClick={() =>
                                                changeStrategy(s.strategy)
                                            }
                                            aria-pressed={
                                                s.strategy === strategyId
                                            }
                                        >
                                            <span
                                                className="h-2 w-2 rounded-full"
                                                style={{
                                                    background:
                                                        STRATEGY_COLORS[
                                                            s.strategy
                                                        ],
                                                }}
                                            />
                                            {STRATEGY_LABELS[s.strategy]}
                                        </button>
                                    </th>
                                    {[
                                        s.recall1,
                                        s.recall5,
                                        s.mrr5,
                                        s.citationAccuracy1,
                                        s.citationPrecision5,
                                        s.groundedness,
                                        s.abstentionAccuracy,
                                        s.falseSupportRate,
                                    ].map((value, i) => (
                                        <td className="mono" key={i}>
                                            {value.toFixed(4)}
                                        </td>
                                    ))}
                                    <td className="mono">
                                        {s.indexEmbedCalls}
                                    </td>
                                    <td className="mono">
                                        {s.averageContextTokens}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
                <div className="mt-4 flex items-start gap-2 rounded-lg bg-muted/60 p-3">
                    <ArrowUpRight
                        size={15}
                        className="mt-0.5 shrink-0 text-primary"
                    />
                    <p className="text-muted text-[11px] leading-5">
                        <strong className="text-foreground">
                            Semantic chunking reduces average context size but
                            requires significantly more embedding operations in
                            this benchmark.
                        </strong>{" "}
                        Its false-support rate is{" "}
                        {percent(
                            dataset.strategies.find(
                                (s) => s.strategy === "semantic",
                            )?.falseSupportRate ?? 0,
                        )}
                        , despite high abstention accuracy. Published answerable
                        / unanswerable z separation is just 0.05. Markdown-aware
                        separates best at 0.59. Compare false support before
                        choosing on recall alone.
                    </p>
                </div>
            </Panel>

            <Panel
                className="mt-5"
                title="Quality vs Cost"
                icon={<ArrowUpRight size={14} />}
                action={
                    <SelectField
                        label="Plot strategies"
                        value={chartScope}
                        onChange={setChartScope}
                        options={[
                            { value: "all", label: "All strategies" },
                            { value: "selected", label: "Active strategy" },
                        ]}
                    />
                }
            >
                <div className="mb-5 grid gap-4 sm:grid-cols-2">
                    <SelectField
                        label="Resource axis"
                        value={resource}
                        onChange={(value) =>
                            setResource(
                                value === "averageContextTokens"
                                    ? "averageContextTokens"
                                    : "indexEmbedCalls",
                            )
                        }
                        options={[
                            {
                                value: "indexEmbedCalls",
                                label: "Index embedding calls",
                            },
                            {
                                value: "averageContextTokens",
                                label: "Context tokens per query",
                            },
                        ]}
                    />
                    <label>
                        <span className="field-label flex justify-between">
                            Simulated top K range{" "}
                            <span className="mono">1–{topK}</span>
                        </span>
                        <input
                            aria-label="Simulated top K range"
                            type="range"
                            min={2}
                            max={20}
                            value={topK}
                            onChange={(event) =>
                                setTopK(Number(event.target.value))
                            }
                            className="mt-4 w-full accent-primary"
                        />
                    </label>
                </div>
                <div className="grid min-w-0 gap-6 lg:grid-cols-2">
                    <TradeoffScatter
                        strategies={chartStrategies}
                        resource={resource}
                    />
                    <LatencyChart strategies={chartStrategies} topK={topK} />
                </div>
                <div className="text-muted mt-4 flex flex-wrap gap-x-4 gap-y-2 text-[10px]">
                    {chartStrategies.map((s) => (
                        <span
                            key={s.strategy}
                            className="flex items-center gap-1.5"
                        >
                            <span
                                className="h-2 w-2 rounded-full"
                                style={{
                                    background: STRATEGY_COLORS[s.strategy],
                                }}
                            />
                            {STRATEGY_LABELS[s.strategy]}
                        </span>
                    ))}
                </div>
                <p className="text-muted mt-3 text-[10px] leading-5">
                    Latency illustration: 105 ms overhead + 0.8 ms per indexed
                    chunk + 13 ms per retrieved chunk + 0.035 ms per estimated
                    context token. This local model explains scaling; it is not
                    a performance forecast. Quality scores stay at the published
                    top K = 5.
                </p>
            </Panel>

            <Panel
                className="mt-5"
                title="Evaluation Questions"
                icon={<Search size={14} />}
                action={
                    <span className="text-muted text-[11px]" aria-live="polite">
                        {filteredCases.length} / {dataset.questionCount} cases ·{" "}
                        {STRATEGY_LABELS[strategyId]}
                    </span>
                }
            >
                <div className="mb-5 grid gap-3 md:grid-cols-[2fr_1fr_1fr_1fr]">
                    <label className="flex flex-col gap-1.5">
                        <span className="field-label">Search cases</span>
                        <div className="relative">
                            <Search
                                size={13}
                                className="text-muted absolute left-3 top-3"
                            />
                            <input
                                className="field w-full !pl-8 text-xs"
                                placeholder="Question, ID, or gold section…"
                                value={search}
                                onChange={(event) => {
                                    setSearch(event.target.value);
                                    setPage(1);
                                }}
                            />
                        </div>
                    </label>
                    <SelectField
                        label="Difficulty"
                        value={difficulty}
                        onChange={(value) => {
                            setDifficulty(value);
                            setPage(1);
                        }}
                        options={[
                            { value: "all", label: "All difficulties" },
                            ...Array.from(
                                new Set(
                                    dataset.questions.map((q) => q.difficulty),
                                ),
                            ).map((d) => ({ value: d, label: d })),
                        ]}
                    />
                    <SelectField
                        label="Question type"
                        value={type}
                        onChange={(value) => {
                            setType(value);
                            setPage(1);
                        }}
                        options={[
                            { value: "all", label: "All types" },
                            ...Array.from(
                                new Set(dataset.questions.map((q) => q.type)),
                            ).map((t) => ({ value: t, label: t })),
                        ]}
                    />
                    <SelectField
                        label="Result"
                        value={status}
                        onChange={(value) => {
                            setStatus(value);
                            setPage(1);
                        }}
                        options={[
                            { value: "all", label: "All results" },
                            ...statuses.map((s) => ({ value: s, label: s })),
                        ]}
                    />
                </div>
                <p className="text-muted mb-3 text-[10px] leading-5">
                    Pass reflects evidence coverage and first-citation
                    correctness. “Abstained correctly” denotes the harness
                    rejecting an unanswerable case; the product does not
                    currently abstain. Retrieved source shows the first
                    retrieved chunk. Case filters affect only this explorer;
                    KPIs and benchmark charts use the complete 50-question
                    published run.
                </p>
                <div className="overflow-x-auto">
                    <table className="data-table w-full text-xs">
                        <caption className="sr-only">
                            Golden questions and {STRATEGY_LABELS[strategyId]}{" "}
                            published outcomes
                        </caption>
                        <thead>
                            <tr>
                                {[
                                    "Case / question",
                                    "Expected source",
                                    "Retrieved source",
                                    "Difficulty",
                                    "Result",
                                    "Citation correct",
                                    "Recall @5",
                                    "Inspect",
                                ].map((label) => (
                                    <th key={label} scope="col">
                                        {label}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {visibleCases.map((question) => {
                                const outcome = outcomeFor(
                                    strategy,
                                    question.id,
                                );
                                return (
                                    <tr key={question.id}>
                                        <td className="min-w-64">
                                            <div className="text-muted mono mb-1 text-[10px]">
                                                {question.id} · {question.type}
                                            </div>
                                            <button
                                                type="button"
                                                onClick={() =>
                                                    setSelectedCase(question)
                                                }
                                                className="rounded text-left font-medium leading-5 hover:text-primary focus-visible:outline-2 focus-visible:outline-primary"
                                            >
                                                {question.question}
                                            </button>
                                        </td>
                                        <td className="mono text-[10px]">
                                            {question.expectedSourceFile ??
                                                "None expected"}
                                        </td>
                                        <td className="mono text-[10px]">
                                            {outcome?.retrievedChunks[0]?.split(
                                                "#",
                                            )[0] ?? "Unavailable"}
                                        </td>
                                        <td>
                                            <Badge
                                                variant="outline"
                                                className="text-[10px]"
                                            >
                                                {question.difficulty}
                                            </Badge>
                                        </td>
                                        <td className="whitespace-nowrap text-[11px]">
                                            {outcome
                                                ? caseStatus(
                                                      outcome,
                                                      question.goldAnchors
                                                          .length > 0,
                                                  )
                                                : "Unavailable"}
                                        </td>
                                        <td>
                                            {outcome &&
                                            question.goldAnchors.length > 0
                                                ? outcome.topCitationCorrect
                                                    ? "Yes"
                                                    : "No"
                                                : "N/A"}
                                        </td>
                                        <td className="mono">
                                            {outcome &&
                                            question.goldAnchors.length > 0
                                                ? percent(outcome.recallAt5)
                                                : "—"}
                                        </td>
                                        <td>
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() =>
                                                    setSelectedCase(question)
                                                }
                                                aria-label={`Inspect ${question.id}: ${question.question}`}
                                            >
                                                <ChevronRight size={15} />
                                            </Button>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
                {filteredCases.length === 0 && (
                    <div className="py-10 text-center">
                        <p className="text-muted text-sm">
                            No cases match these filters.
                        </p>
                        <Button
                            className="mt-3"
                            variant="outline"
                            size="sm"
                            onClick={() => {
                                setSearch("");
                                setDifficulty("all");
                                setType("all");
                                setStatus("all");
                                setPage(1);
                            }}
                        >
                            Reset filters
                        </Button>
                    </div>
                )}
                <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
                    <p className="text-muted text-[11px]" aria-live="polite">
                        Showing {filteredCases.length ? pageStart + 1 : 0}–
                        {Math.min(pageStart + pageSize, filteredCases.length)}{" "}
                        of {filteredCases.length} cases
                    </p>
                    <div className="flex items-center gap-3">
                        <Button
                            variant="outline"
                            size="sm"
                            disabled={currentPage === 1}
                            onClick={() => setPage(currentPage - 1)}
                        >
                            Previous
                        </Button>
                        <span className="text-muted mono text-[11px]">
                            Page {currentPage} of {pageCount}
                        </span>
                        <Button
                            variant="outline"
                            size="sm"
                            disabled={currentPage === pageCount}
                            onClick={() => setPage(currentPage + 1)}
                        >
                            Next
                        </Button>
                    </div>
                </div>
            </Panel>
            <Drawer
                open={selectedCase !== null}
                onOpenChange={(open) => {
                    if (!open) setSelectedCase(null);
                }}
                title={
                    selectedCase
                        ? `${selectedCase.id} · ${selectedCase.difficulty}`
                        : "Case details"
                }
                description="Published golden evidence and per-strategy retrieval outcomes."
            >
                {selectedCase && (
                    <CaseDetail
                        question={selectedCase}
                        outcome={selectedOutcome}
                        strategy={strategy}
                        onStrategyChange={changeStrategy}
                    />
                )}
            </Drawer>
            <PageFooter />
        </>
    );
}

function CaseDetail({
    question,
    outcome,
    strategy,
    onStrategyChange,
}: {
    question: EvaluationCase;
    outcome?: EvaluationOutcome;
    strategy: StrategyBenchmark;
    onStrategyChange: (value: string) => void;
}) {
    return (
        <div className="space-y-6 pt-5">
            <div>
                <div className="eyebrow mb-2">
                    {question.type} ·{" "}
                    {question.expectedSourceFile ?? "No answer expected"}
                </div>
                <h3 className="text-base font-semibold leading-6">
                    {question.question}
                </h3>
                {question.notes && (
                    <p className="text-muted mt-2 text-xs leading-5">
                        {question.notes}
                    </p>
                )}
            </div>
            <SelectField
                label="Inspect strategy"
                value={strategy.strategy}
                onChange={onStrategyChange}
                options={dataset.strategies.map((s) => ({
                    value: s.strategy,
                    label: STRATEGY_LABELS[s.strategy],
                }))}
            />
            <div>
                <h4 className="mb-3 flex items-center gap-2 text-xs font-semibold">
                    <CheckCircle2 size={14} />
                    Expected evidence
                </h4>
                {question.goldAnchors.length ? (
                    question.goldAnchors.map((anchor, i) => (
                        <div
                            key={i}
                            className="mb-2 rounded-lg border bg-muted/30 p-3"
                        >
                            <div className="text-muted mb-1.5 text-[10px]">
                                {anchor.section}
                            </div>
                            <blockquote className="text-xs leading-5">
                                “{anchor.phrase}”
                            </blockquote>
                        </div>
                    ))
                ) : (
                    <p className="text-muted text-xs leading-5">
                        Unanswerable case: no matching evidence exists in the
                        golden corpus. The harness expects the support
                        classifier to reject this case.
                    </p>
                )}
                {question.answerKeywords.length > 0 && (
                    <p className="text-muted mt-2 text-[11px]">
                        Expected keywords: {question.answerKeywords.join(", ")}
                    </p>
                )}
                {question.absentTerms.length > 0 && (
                    <p className="text-muted mt-2 text-[11px]">
                        Absent terms: {question.absentTerms.join(", ")}
                    </p>
                )}
            </div>
            {outcome && (
                <>
                    <div className="grid grid-cols-2 gap-3">
                        {[
                            {
                                label: "Recall @1",
                                value: question.goldAnchors.length
                                    ? percent(outcome.recallAt1)
                                    : "N/A",
                            },
                            {
                                label: "Recall @5",
                                value: question.goldAnchors.length
                                    ? percent(outcome.recallAt5)
                                    : "N/A",
                            },
                            {
                                label: "Reciprocal rank",
                                value: outcome.reciprocalRank.toFixed(4),
                            },
                            {
                                label: "Citation precision",
                                value: percent(outcome.citationPrecision),
                            },
                            {
                                label: "Support z-score",
                                value: outcome.supportZScore.toFixed(4),
                            },
                            {
                                label: "Context characters",
                                value: outcome.contextChars.toLocaleString(),
                            },
                        ].map((metric) => (
                            <div
                                key={metric.label}
                                className="rounded-lg border p-3"
                            >
                                <div className="text-muted text-[10px]">
                                    {metric.label}
                                </div>
                                <div className="mono mt-1.5 text-base font-semibold">
                                    {metric.value}
                                </div>
                            </div>
                        ))}
                    </div>
                    <div className="rounded-lg border p-3 text-xs leading-6">
                        <p>
                            Evidence:{" "}
                            <strong>
                                {caseStatus(
                                    outcome,
                                    question.goldAnchors.length > 0,
                                )}
                            </strong>
                        </p>
                        <p>
                            First citation:{" "}
                            <strong>
                                {outcome.topCitationCorrect
                                    ? "Correct"
                                    : "Not relevant"}
                            </strong>
                        </p>
                        <p>
                            Harness support policy:{" "}
                            <strong>
                                {outcome.supported ? "Supported" : "Rejected"}
                            </strong>
                        </p>
                        <p className="text-muted mt-1 text-[11px]">
                            A harness decision, not an actual product
                            abstention. Groundedness{" "}
                            {percent(outcome.groundedness)} uses copied context.
                        </p>
                    </div>
                    <div>
                        <h4 className="mb-3 text-xs font-semibold">
                            Retrieved chunk references
                        </h4>
                        <ol className="space-y-2">
                            {outcome.retrievedChunks.map((chunk, i) => (
                                <li
                                    key={`${chunk}-${i}`}
                                    className="flex items-center gap-3 rounded-lg border px-3 py-2.5"
                                >
                                    <span className="text-muted mono text-[10px]">
                                        {String(i + 1).padStart(2, "0")}
                                    </span>
                                    <span className="mono text-xs">
                                        {chunk}
                                    </span>
                                </li>
                            ))}
                        </ol>
                    </div>
                </>
            )}
            <div className="space-y-4 rounded-lg border border-dashed bg-muted/30 p-4">
                <div>
                    <h4 className="text-xs font-semibold">
                        Expected answer · golden-anchor extract
                    </h4>
                    <p className="text-muted mt-2 text-xs leading-5">
                        {question.goldAnchors.length
                            ? question.goldAnchors
                                  .map((anchor) => anchor.phrase)
                                  .join(" · ")
                            : "No answer expected: the question is unanswerable from this corpus."}
                    </p>
                    <p className="text-muted mt-1 text-[10px]">
                        These benchmark anchors describe expected evidence, not
                        a separately authored reference answer.
                    </p>
                </div>
                <div>
                    <h4 className="text-xs font-semibold">Generated answer</h4>
                    <p className="text-muted mt-2 text-xs leading-5">
                        Unavailable. The published report does not include
                        generated answer text.
                    </p>
                </div>
                <div>
                    <h4 className="text-xs font-semibold">
                        Expected citations
                    </h4>
                    {question.goldAnchors.length ? (
                        <ul className="text-muted mt-2 space-y-1 text-xs">
                            {question.goldAnchors.map((anchor, i) => (
                                <li key={i}>
                                    {question.expectedSourceFile} ·{" "}
                                    {anchor.section}
                                </li>
                            ))}
                        </ul>
                    ) : (
                        <p className="text-muted mt-2 text-xs">
                            None expected.
                        </p>
                    )}
                </div>
                <div>
                    <h4 className="text-xs font-semibold">
                        Generated citations
                    </h4>
                    <p className="text-muted mt-2 text-xs leading-5">
                        Rendered answer citations are unavailable in the
                        artifact. The retrieved chunk references above are the
                        scored citation candidates; they do not establish the
                        citations actually displayed in an answer.
                    </p>
                </div>
                <div>
                    <h4 className="text-xs font-semibold">
                        Full retrieved chunk text
                    </h4>
                    <p className="text-muted mt-2 text-xs leading-5">
                        Unavailable in the published report. Golden anchors are
                        not reconstructed retrieved chunks.
                    </p>
                </div>
            </div>
        </div>
    );
}
