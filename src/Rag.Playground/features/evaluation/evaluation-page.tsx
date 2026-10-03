"use client";
import {
    ArrowUpRight,
    FlaskConical,
    Search,
    SlidersHorizontal,
    Info,
    ChevronRight,
    Database,
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
    METRICS,
    percent,
} from "@/lib/evaluation/data";
import {
    BenchmarkBars,
    LatencyChart,
    TradeoffScatter,
} from "./evaluation-charts";

import { CaseDetail, SelectField } from "./case-detail";

import { dataset, statuses, outcomeFor, useEvaluation } from "./use-evaluation";

export function EvaluationPage() {
    const {
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
    } = useEvaluation();
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
                    <strong>Read these scores in context.</strong> Lexical
                    overlap diagnostic is 100% by construction: the
                    deterministic client copies a sentence from retrieved
                    context. Abstention accuracy scores the harness support
                    policy (z ≥ {EVALUATION_PROFILE.supportZThreshold}). This
                    legacy benchmark does not exercise product abstention; the
                    workbench declines queries when no context is admitted.
                    These scores do not measure production model answer quality.
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
                                    "Lexical overlap diagnostic",
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
                    <details className="rounded-lg border p-4">
                        <summary className="cursor-pointer text-xs">
                            Illustrative latency explainer (not a benchmark)
                        </summary>
                        <LatencyChart
                            strategies={chartStrategies}
                            topK={topK}
                        />
                    </details>
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
                    correctness. “Harness rejected unsupported case” denotes the
                    harness rejecting an unanswerable case; the product does not
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
