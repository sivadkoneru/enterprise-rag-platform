import type {
    EvaluationDataset,
    EvaluationOutcome,
    StrategyBenchmark,
} from "@/lib/contracts";
import published from "./fixtures/latest.json";
import golden from "./fixtures/golden.json";

/** Local snapshots of the checked-in report; no evaluation runs or network calls. */
export function getEvaluationDataset(): EvaluationDataset {
    return {
        questionCount: published.questionCount,
        strategies: published.strategies,
        questions: golden.questions,
    };
}

export const EVALUATION_PROFILE = published.profile;
export const METRICS = [
    {
        key: "recall1",
        label: "Recall @1",
        definition:
            "Share of gold evidence anchors found in the first retrieved chunk. Averaged over answerable cases.",
    },
    {
        key: "recall5",
        label: "Recall @5",
        definition:
            "Share of gold evidence anchors recovered in the first five chunks. Partial coverage receives partial credit.",
    },
    {
        key: "mrr5",
        label: "MRR @5",
        definition:
            "Mean reciprocal rank of the first relevant chunk, up to rank five. Higher means relevant evidence appears sooner.",
    },
    {
        key: "citationAccuracy1",
        label: "Citation accuracy",
        definition:
            "Share of answerable cases whose first citation points to relevant evidence.",
    },
    {
        key: "citationPrecision5",
        label: "Citation precision",
        definition:
            "Fraction of the top five retrieved citations that contain gold evidence. Relevant context may coexist with distractors.",
    },
    {
        key: "groundedness",
        label: "Groundedness",
        definition:
            "Supported-token fraction. Always 1.000 here because the deterministic client copies a sentence from context; this is a regression tripwire, not evidence of live model quality.",
    },
    {
        key: "abstentionAccuracy",
        label: "Abstention accuracy",
        definition:
            "Harness classification at support z ≥ 2.5. The product does not abstain today. Read alongside false-support rate; this is not measured product answer behavior.",
    },
] satisfies {
    key: keyof StrategyBenchmark;
    label: string;
    definition: string;
}[];

export function percent(value: number): string {
    return `${(value * 100).toFixed(1)}%`;
}
export function caseStatus(
    outcome: EvaluationOutcome,
    answerable: boolean,
): string {
    if (!answerable)
        return outcome.supported ? "False support" : "Abstained correctly";
    if (outcome.recallAt5 === 0) return "Retrieval miss";
    if (!outcome.topCitationCorrect) return "Citation mismatch";
    return outcome.fullyCovered ? "Pass" : "Partial coverage";
}
export function latencyProjection(
    strategy: StrategyBenchmark,
    topK: number,
): number {
    // Explicit illustration, not a benchmark: fixed overhead + retrieval + context processing.
    return Math.round(
        105 +
            strategy.chunkCount * 0.8 +
            topK * 13 +
            (strategy.averageContextTokens / 5) * topK * 0.035,
    );
}
