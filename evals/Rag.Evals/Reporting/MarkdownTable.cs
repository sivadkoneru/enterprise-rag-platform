using System.Globalization;
using System.Text;
using Rag.Evals.Scoring;

namespace Rag.Evals.Reporting;

/// <summary>Renders the published tables. Pure formatting; every number arrives pre-rounded.</summary>
internal static class MarkdownTable
{
    public static string BenchmarkTable(EvalRun run)
    {
        var builder = new StringBuilder();
        builder.Append("| Strategy | Chunks | Avg chars | p95 chars | Index embed calls | Recall@1 | Recall@5 | Full-cov@5 | MRR@5 | Citation acc@1 | Citation prec@5 | Answer-kw | Fragmented | Ctx tokens@5 |\n");
        builder.Append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |\n");

        foreach (var report in run.Strategies)
        {
            builder.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"| `{report.Strategy}` | {report.ChunkCount} | {report.AverageChunkChars:F0} | {report.P95ChunkChars} | {report.IndexEmbedCalls} | {report.Recall1:F3} | {report.Recall5:F3} | {report.FullCoverage5:F3} | {report.Mrr5:F3} | {report.CitationAccuracy1:F3} | {report.CitationPrecision5:F3} | {report.AnswerKeywordHitRate:F3} | {report.FragmentedAnchorRate:F3} | {report.AverageContextTokens} |\n"));
        }

        return builder.ToString();
    }

    public static string DifficultyTable(EvalRun run)
    {
        var bands = new[] { "easy", "medium", "hard" };
        var builder = new StringBuilder();
        builder.Append("| Strategy | Recall@5 easy | Recall@5 medium | Recall@5 hard | MRR@5 easy | MRR@5 medium | MRR@5 hard |\n");
        builder.Append("| --- | ---: | ---: | ---: | ---: | ---: | ---: |\n");

        foreach (var report in run.Strategies)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| `{report.Strategy}` ");
            foreach (var band in bands)
            {
                builder.Append(CultureInfo.InvariantCulture, $"| {Value(report.RecallByDifficulty, band):F3} ");
            }

            foreach (var band in bands)
            {
                builder.Append(CultureInfo.InvariantCulture, $"| {Value(report.MrrByDifficulty, band):F3} ");
            }

            builder.Append("|\n");
        }

        return builder.ToString();
    }

    public static string IntegrityTable(EvalRun run)
    {
        var builder = new StringBuilder();
        builder.Append("| Strategy | Citation integrity | Groundedness | Mean z (answerable) | Mean z (unanswerable) | z separation | Abstention acc | False-support |\n");
        builder.Append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |\n");
        foreach (var report in run.Strategies)
        {
            builder.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"| `{report.Strategy}` | {report.CitationIntegrity:F3} | {report.Groundedness:F3} | {report.AnswerableMeanZ:F2} | {report.UnanswerableMeanZ:F2} | {report.AnswerableMeanZ - report.UnanswerableMeanZ:F2} | {report.AbstentionAccuracy:F3} | {report.FalseSupportRate:F3} |\n"));
        }

        return builder.ToString();
    }

    public static string Profile(EvalRun run)
    {
        var profile = run.Profile;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Run profile: {run.QuestionCount} questions, chunk size {profile.ChunkSize}, overlap {profile.ChunkOverlap}, semantic distance threshold {profile.SemanticDistanceThreshold}, top-k {profile.TopK}, embedding dimensions {profile.EmbeddingDimensions}, provider `{profile.LlmProvider}`, corpus {string.Join(" + ", profile.Corpus)}.\n");
    }

    private static double Value(IReadOnlyDictionary<string, double> source, string key)
    {
        return source.TryGetValue(key, out var value) ? value : 0;
    }

}
