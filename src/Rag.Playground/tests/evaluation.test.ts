import { describe, expect, it } from "vitest";
import type { EvaluationOutcome } from "../lib/contracts";
import {
    caseStatus,
    getEvaluationDataset,
    METRICS,
} from "../lib/evaluation/data";

const dataset = getEvaluationDataset();
const sample = dataset.strategies[0].outcomes[0];
const outcome = (overrides: Partial<EvaluationOutcome>): EvaluationOutcome => ({
    ...sample,
    ...overrides,
});

describe("published evaluation dataset", () => {
    it("joins all 50 unique questions with one outcome per strategy", () => {
        expect(dataset.questionCount).toBe(50);
        const questionIds = dataset.questions.map((question) => question.id);
        expect(new Set(questionIds).size).toBe(50);
        for (const strategy of dataset.strategies) {
            expect(strategy.outcomes).toHaveLength(50);
            expect(
                strategy.outcomes.map((result) => result.questionId).sort(),
            ).toEqual([...questionIds].sort());
            for (const result of strategy.outcomes) {
                expect(result.recallAt5).toBeGreaterThanOrEqual(0);
                expect(result.recallAt5).toBeLessThanOrEqual(1);
                expect(result.retrievedChunks).toHaveLength(5);
            }
        }
    });

    it("preserves the published semantic resource and quality tradeoff", () => {
        const semantic = dataset.strategies.find(
            (strategy) => strategy.strategy === "semantic",
        );
        expect(semantic).toMatchObject({
            recall5: 0.619,
            indexEmbedCalls: 310,
            averageContextTokens: 212,
            falseSupportRate: 0.75,
        });
        const others = dataset.strategies.filter(
            (strategy) => strategy.strategy !== "semantic",
        );
        expect(
            others.every((strategy) => strategy.recall5 > semantic!.recall5),
        ).toBe(true);
        expect(
            others.every(
                (strategy) =>
                    strategy.indexEmbedCalls < semantic!.indexEmbedCalls,
            ),
        ).toBe(true);
        expect(
            others.every(
                (strategy) =>
                    strategy.averageContextTokens >
                    semantic!.averageContextTokens,
            ),
        ).toBe(true);
    });

    it("maps seven KPI definitions to numeric published fields", () => {
        expect(METRICS.map((metric) => metric.key)).toEqual([
            "recall1",
            "recall5",
            "mrr5",
            "citationAccuracy1",
            "citationPrecision5",
            "groundedness",
            "abstentionAccuracy",
        ]);
        for (const metric of METRICS) {
            expect(metric.definition.length).toBeGreaterThan(20);
            for (const strategy of dataset.strategies)
                expect(typeof strategy[metric.key]).toBe("number");
        }
        expect(
            METRICS.find((metric) => metric.key === "groundedness")?.definition,
        ).toContain("copies a sentence");
        expect(
            METRICS.find((metric) => metric.key === "abstentionAccuracy")
                ?.definition,
        ).toContain("product does not abstain");
    });
});

describe("case result semantics", () => {
    it("distinguishes full evidence, partial coverage, misses, and citation mismatches", () => {
        expect(
            caseStatus(
                outcome({
                    recallAt5: 1,
                    fullyCovered: true,
                    topCitationCorrect: true,
                }),
                true,
            ),
        ).toBe("Pass");
        expect(
            caseStatus(
                outcome({
                    recallAt5: 0.5,
                    fullyCovered: false,
                    topCitationCorrect: true,
                }),
                true,
            ),
        ).toBe("Partial coverage");
        expect(
            caseStatus(
                outcome({
                    recallAt5: 0,
                    fullyCovered: false,
                    topCitationCorrect: false,
                }),
                true,
            ),
        ).toBe("Retrieval miss");
        expect(
            caseStatus(
                outcome({
                    recallAt5: 1,
                    fullyCovered: true,
                    topCitationCorrect: false,
                }),
                true,
            ),
        ).toBe("Citation mismatch");
    });

    it("scores unanswerable cases using the harness support decision", () => {
        expect(caseStatus(outcome({ supported: false }), false)).toBe(
            "Abstained correctly",
        );
        expect(caseStatus(outcome({ supported: true }), false)).toBe(
            "False support",
        );
    });

    it("does not mistake a harness rejection for a product abstention on answerable evidence", () => {
        expect(
            caseStatus(
                outcome({
                    supported: false,
                    recallAt5: 1,
                    fullyCovered: true,
                    topCitationCorrect: true,
                }),
                true,
            ),
        ).toBe("Pass");
    });
});
