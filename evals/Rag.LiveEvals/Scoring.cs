using Rag.Core.Workbench;

namespace Rag.LiveEvals;

public static class Scoring
{
    public const string Version = "source-spans-v1";
    public static readonly int[] Depths = [1, 3, 5];
    public static readonly string[] Modes = ["vector", "hybrid"];
    // Source qualification is mandatory. Same words in another policy are not relevant evidence.
    public static RetrievalMetrics Retrieval(EvaluationCase question, IReadOnlyList<DetailedCandidate> ranked, IReadOnlyList<DetailedCandidate> judgedPool, int k, IReadOnlyDictionary<string, ChunkSpan>? spans = null)
    {
        if (!question.Answerable)
        {
            return new(null, null, null, null, "Unanswerable cases are excluded from retrieval averages.");
        }

        if (question.Anchors.Count == 0)
        {
            return new(null, null, null, null, "No gold anchors.");
        }

        var top = ranked.Take(k).ToArray();
        bool Contains(DetailedCandidate chunk, EvidenceAnchor anchor) => spans is not null && spans.TryGetValue(chunk.Id, out var span)
            ? span.Source == anchor.Source && span.Start <= anchor.Start && span.End >= anchor.End
            : chunk.Filename == anchor.Source && chunk.Content.Contains(anchor.Text, StringComparison.Ordinal);
        int Grade(DetailedCandidate chunk)
        {
            if (spans is not null && spans.TryGetValue(chunk.Id, out var span))
            {
                return question.Anchors.Any(anchor => anchor.Source == span.Source && span.Start <= anchor.Start && span.End >= anchor.End) ? 2
                    : question.Anchors.Any(anchor => anchor.Source == span.Source && Math.Max(span.Start, anchor.Start) < Math.Min(span.End, anchor.End)) ? 1 : 0;
            }
            return question.Anchors.Any(anchor => Contains(chunk, anchor)) ? 2 : 0;
        }
        var recall = question.Anchors.Count(anchor => top.Any(chunk => Contains(chunk, anchor))) / (double)question.Anchors.Count;
        double? fragmented = null;
        if (spans is not null)
        {
            var fragmentedCount = 0;
            foreach (var anchor in question.Anchors)
            {
                if (top.Any(chunk => Contains(chunk, anchor))) { continue; }
                var coveredTo = anchor.Start;
                foreach (var span in top.Select(chunk => spans.GetValueOrDefault(chunk.Id)).OfType<ChunkSpan>().Where(span => span.Source == anchor.Source).OrderBy(span => span.Start))
                {
                    if (span.Start <= coveredTo) { coveredTo = Math.Max(coveredTo, span.End); }
                }
                if (coveredTo >= anchor.End) { fragmentedCount++; }
            }
            fragmented = fragmentedCount / (double)question.Anchors.Count;
        }
        var first = Array.FindIndex(top, chunk => Grade(chunk) == 2);
        static double Dcg(IEnumerable<int> grades) => grades.Select((grade, index) => (Math.Pow(2, grade) - 1) / Math.Log2(index + 2)).Sum();
        var ideal = Dcg(judgedPool.Select(Grade).OrderDescending().Take(k));
        return new(recall, first < 0 ? 0 : 1.0 / (first + 1), ideal > 0 ? Dcg(top.Select(Grade)) / ideal : null, recall == 1 ? 1 : 0, ideal > 0 ? null : "No judged chunk fully or partially resolves an anchor for this profile.", fragmented);
    }
    public static double? Percentile(IEnumerable<double> values, double quantile)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? null : sorted[Math.Clamp((int)Math.Ceiling(quantile * sorted.Length) - 1, 0, sorted.Length - 1)];
    }
    public static void Validate(SemanticJudgment result)
    {
        if (result.Claims < 0 || result.RequiredFacts < 0 || result.Citations < 0 ||
            result.SupportedClaims < 0 || result.SupportedClaims > result.Claims || result.CorrectClaims < 0 || result.CorrectClaims > result.Claims ||
            result.CoveredFacts < 0 || result.CoveredFacts > result.RequiredFacts || result.Contradictions < 0 || result.Contradictions > result.Claims ||
            result.SupportedCitations < 0 || result.SupportedCitations > result.Citations || result.CitedClaims < 0 || result.CitedClaims > result.Claims || string.IsNullOrWhiteSpace(result.Explanation))
        { throw new InvalidDataException("Judge returned inconsistent counts."); }
    }
}
