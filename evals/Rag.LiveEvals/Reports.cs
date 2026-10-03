using System.Text.Json;

namespace Rag.LiveEvals;

public static class Reports
{
    private static double? Ratio(int numerator, int denominator) => denominator == 0 ? null : (double)numerator / denominator;
    public static async Task WriteAsync(string directory, IReadOnlyList<CaseResult> results, IReadOnlyList<EvaluationCase> questions, decimal reserved)
    {
        var groups = results.GroupBy(item => item.Mode).Select(group =>
        {
            var completed = group.Where(item => item.Run is not null).ToArray();
            var judged = completed.Where(item => item.Judge is not null).ToArray();
            var judgments = judged.Select(item => item.Judge!).ToArray();
            var tp = judged.Count(item => !questions.Single(q => q.Id == item.Id).Answerable && !item.Judge!.SubstantiveAnswer);
            var fp = judged.Count(item => questions.Single(q => q.Id == item.Id).Answerable && !item.Judge!.SubstantiveAnswer);
            var fn = judged.Count(item => !questions.Single(q => q.Id == item.Id).Answerable && item.Judge!.SubstantiveAnswer);
            var tn = judged.Count(item => questions.Single(q => q.Id == item.Id).Answerable && item.Judge!.SubstantiveAnswer);
            var precision = Ratio(judgments.Sum(j => j.CorrectClaims), judgments.Sum(j => j.Claims));
            var recall = Ratio(judgments.Sum(j => j.CoveredFacts), judgments.Sum(j => j.RequiredFacts));
            return new
            {
                mode = group.Key,
                requested = group.Count(),
                succeeded = completed.Length,
                failed = group.Count(item => item.Status == "failed"),
                skipped = group.Count(item => item.Status == "budget-exhausted"),
                retrieval = Scoring.Depths.ToDictionary(k => k, k => new
                {
                    evidenceAnchorRecall = completed.Select(item => item.Retrieval![k].Recall).Average(),
                    mrr = completed.Select(item => item.Retrieval![k].Mrr).Average(),
                    ndcg = completed.Select(item => item.Retrieval![k].Ndcg).Average(),
                    fragmentedEvidence = completed.Select(item => item.Retrieval![k].FragmentedEvidence).Average(),
                    fullCoverage = completed.Select(item => item.Retrieval![k].FullCoverage).Average(),
                    judgedCases = completed.Count(item => item.Retrieval![k].Ndcg is not null)
                }),
                semantic = new
                {
                    judged = judgments.Length,
                    missingReason = judgments.Length == 0 ? "No successful semantic judgments." : null,
                    faithfulness = Ratio(judgments.Sum(j => j.SupportedClaims), judgments.Sum(j => j.Claims)),
                    answerPrecision = precision,
                    requiredFactRecall = recall,
                    answerF1 = precision is not null && recall is not null ? precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0 : null,
                    contradictionRate = Ratio(judgments.Sum(j => j.Contradictions), judgments.Sum(j => j.Claims)),
                    semanticCitationSupport = Ratio(judgments.Sum(j => j.SupportedCitations), judgments.Sum(j => j.Citations)),
                    factualClaimCitationCoverage = Ratio(judgments.Sum(j => j.CitedClaims), judgments.Sum(j => j.Claims)),
                    humanAgreement = (double?)null,
                    humanAgreementReason = "Complete human-review.jsonl before publishing semantic scores."
                },
                referenceResolution = Ratio(completed.Sum(item => item.Run!.Citations.Count(c => c.Valid)), completed.Sum(item => item.Run!.Citations.Count)),
                abstention = new { truePositive = tp, falsePositive = fp, falseNegative = fn, trueNegative = tn, precision = Ratio(tp, tp + fp), recall = Ratio(tp, tp + fn), unsupportedAnswerRate = Ratio(fn, tp + fn), falseAbstentionRate = Ratio(fp, fp + tn), unjudged = completed.Length - judged.Length },
                latency = new
                {
                    samples = completed.Length,
                    p50 = Scoring.Percentile(completed.Select(item => item.DurationMs), .5),
                    p95 = Scoring.Percentile(completed.Select(item => item.DurationMs), .95),
                    failedDurationsMs = group.Where(item => item.Status == "failed").Select(item => item.DurationMs),
                    stages = completed.SelectMany(item => item.Run!.Trace).GroupBy(stage => stage.Id).Select(stages => new { id = stages.Key, samples = stages.Count(), p50 = Scoring.Percentile(stages.Select(stage => (double)stage.DurationMs), .5), p95 = Scoring.Percentile(stages.Select(stage => (double)stage.DurationMs), .95) })
                },
                usage = new { providerPromptTokens = completed.Any(item => item.Run!.PromptTokens is not null) ? completed.Sum(item => item.Run!.PromptTokens) : null, providerEmbeddingTokens = completed.Any(item => item.Run!.EmbeddingTokens is not null) ? completed.Sum(item => item.Run!.EmbeddingTokens) : null, missingPromptUsage = completed.Count(item => item.Run!.PromptTokens is null), missingEmbeddingUsage = completed.Count(item => item.Run!.EmbeddingTokens is null) },
                cost = new { averageQueryUsd = completed.Select(item => item.QueryEstimatedUsd).Average(), querySamples = completed.Count(item => item.QueryEstimatedUsd is not null), estimatedJudgeUsd = completed.Any(item => item.JudgeEstimatedUsd is not null) ? completed.Sum(item => item.JudgeEstimatedUsd) : null, reservedUsd = group.Sum(item => item.ReservedUsd), caveat = "Query cost uses final successful provider usage; reservations include retries. Missing usage stays null." }
            };
        }).ToArray();
        await File.WriteAllTextAsync(Path.Combine(directory, "cases.jsonl"), string.Join("\n", results.Select(item => JsonSerializer.Serialize(item, JsonSerializerOptions.Web))) + "\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "aggregate.json"), JsonSerializer.Serialize(new { schemaVersion = 1, reservedUsdIncludingIndexing = reserved, groups }, LiveApi.Json));
        var review = results.Take(5).Concat(results.Where(item => item.Status != "complete" || item.JudgeFailure is not null || item.Judge is { Contradictions: > 0 } || item.Judge is { } judge && judge.SupportedClaims < judge.Claims)).DistinctBy(item => (item.Id, item.Mode));
        await File.WriteAllTextAsync(Path.Combine(directory, "human-review.jsonl"), string.Join("\n", review.Select(item => JsonSerializer.Serialize(new { item.Id, item.Mode, reviewer = (string?)null, agreed = (bool?)null, correctedJudgment = (SemanticJudgment?)null, notes = "Review answer, admitted evidence and judge explanations before publication." }))));
        var lines = groups.Select(group => $"| {group.mode} | {group.succeeded}/{group.requested} | {group.failed} | {group.latency.p50:F1} | {group.latency.p95:F1} | {group.cost.averageQueryUsd:F6} | {group.semantic.judged} |");
        await File.WriteAllTextAsync(Path.Combine(directory, "summary.md"), "# Measured evaluation\n\nSee manifest.json for models, corpus, pricing, commit and limitations. Null means unavailable, never zero. Semantic scores are judge estimates pending human review. All failures are retained in cases.jsonl.\n\n| Mode | Completed/requested | Failed | p50 ms | p95 ms | Mean estimated query USD | Judged |\n|---|---:|---:|---:|---:|---:|---:|\n" + string.Join("\n", lines) + $"\n\nEstimated reservations including operator-supplied indexing: ${reserved:F4}. No universal quality threshold was selected from this held-out run.\n");
    }
}
