import type { LiveEvaluationMetrics, EvaluationRun } from "@/lib/live/contracts";
export const metricDefinitions: {
    key: keyof LiveEvaluationMetrics;
    label: string;
    definition: string;
}[] = [
    {
        key: "recallAt1",
        label: "Recall@1",
        definition:
            "Share of expected evidence anchors covered by the first retrieved chunk.",
    },
    {
        key: "recallAt5",
        label: "Recall@5",
        definition:
            "Share of expected evidence anchors covered by the first five retrieved chunks.",
    },
    {
        key: "mrrAt5",
        label: "MRR@5",
        definition:
            "Mean reciprocal rank of the first relevant result, truncated at five.",
    },
    {
        key: "citationAccuracy",
        label: "Citation Accuracy",
        definition:
            "Whether the first generated citation points to expected evidence; aggregated across labeled answerable cases.",
    },
    {
        key: "citationPrecision",
        label: "Citation Precision",
        definition:
            "Share of emitted citations whose admitted source contains expected evidence.",
    },
    {
        key: "groundedness",
        label: "Lexical overlap diagnostic",
        definition:
            "Fraction of answer word occurrences also present in admitted context. A lexical proxy, not an entailment or factuality judgment.",
    },
    {
        key: "abstentionAccuracy",
        label: "Abstention Accuracy",
        definition:
            "Agreement between actual abstention and the dataset’s expected abstention labels.",
    },
];
export const percentage = (value: number | null | undefined) =>
    value == null ? "—" : `${(value * 100).toFixed(1)}%`;
export type Outcome = EvaluationRun["profiles"][number]["outcomes"][number];
