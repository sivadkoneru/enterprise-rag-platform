"use client";

import {
    CheckCircle2 } from "lucide-react";





import { STRATEGY_LABELS } from "@/lib/constants";
import {
    caseStatus,
    getEvaluationDataset,
    percent } from "@/lib/evaluation/data";
import type {
    EvaluationCase,
    EvaluationOutcome,
    StrategyBenchmark } from "@/lib/contracts";



const dataset = getEvaluationDataset();
export function SelectField({
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

export function CaseDetail({
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
                            abstention. Lexical overlap diagnostic{" "}
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
