using System.Text.RegularExpressions;

namespace Rag.Core.Workbench;

public static class WorkbenchEvaluation
{
    public const string GroundednessCaveat = "Lexical token-overlap proxy: answer word occurrences present in admitted context. This is not semantic entailment or a live model judge score.";
    public static void Validate(LiveEvaluationRequest request)
    {
        DetailedQueryPipeline.Validate(new DetailedQueryRequest("validation", "validation", "validation", request.TopK, request.Mode, request.Reranker, request.MinRelevance, request.Neighbors, request.MaxContextTokens));
        if (request.ProfileIds is not { Count: > 0 and <= 8 } || request.ProfileIds.Distinct().Count() != request.ProfileIds.Count || request.Questions is not { Count: > 0 and <= 500 } || request.TopK is < 1 or > 100)
        {
            throw new ArgumentException("Choose 1–8 unique profiles, 1–500 questions, and top K between 1 and 100.");
        }

        if (request.Questions.Select(question => question.Id).Distinct(StringComparer.Ordinal).Count() != request.Questions.Count)
        {
            throw new ArgumentException("Evaluation question IDs must be unique.");
        }

        foreach (var question in request.Questions)
        {
            if (string.IsNullOrWhiteSpace(question.Id) || question.Id.Length > 100 || string.IsNullOrWhiteSpace(question.Question) || question.Question.Length > 8000 || question.GoldAnchors is null || question.GoldAnchors.Any(anchor => string.IsNullOrWhiteSpace(anchor.Phrase) || anchor.Phrase.Length > 16000))
            {
                throw new ArgumentException("Questions require a stable ID, text, and valid evidence anchors.");
            }

            if (question.ExpectedAbstention && question.GoldAnchors.Count != 0 || !question.ExpectedAbstention && question.GoldAnchors.Count == 0)
            {
                throw new ArgumentException("Answerable questions require gold anchors; unanswerable questions require expectedAbstention=true and no anchors.");
            }
        }
    }
    public static LiveEvaluationOutcome Score(LiveEvaluationQuestion question, DetailedRun run)
    {
        var ranked = run.Candidates.OrderBy(candidate => candidate.RankAfter).Take(5).ToArray();
        bool SourceMatches(DetailedCandidate chunk) => question.ExpectedSourceFile is null || string.Equals(chunk.Filename, question.ExpectedSourceFile, StringComparison.OrdinalIgnoreCase);
        bool Relevant(DetailedCandidate chunk) => SourceMatches(chunk) && question.GoldAnchors.Any(anchor => Contains(chunk.Content, anchor.Phrase));
        double? Recall(int k) => question.GoldAnchors.Count == 0 ? null : question.GoldAnchors.Count(anchor => ranked.Take(k).Any(chunk => SourceMatches(chunk) && Contains(chunk.Content, anchor.Phrase))) / (double)question.GoldAnchors.Count;
        var firstRank = Array.FindIndex(ranked, Relevant) + 1;
        var citationChunks = run.Citations.Select(citation => run.Context.FirstOrDefault(chunk => chunk.Id == citation.ChunkId && citation.Valid)).OfType<DetailedCandidate>().ToArray();
        var firstReference = run.Citations.Count == 0 ? null : run.Citations[0];
        var firstCitation = firstReference is { Valid: true } ? run.Context.FirstOrDefault(chunk => chunk.Id == firstReference.ChunkId) : null;
        var evidenceWords = DetailedQueryPipeline.Words(string.Join(" ", run.Context.Select(chunk => chunk.Content))).ToHashSet(StringComparer.Ordinal);
        var answerWithoutCitations = Regex.Replace(run.Answer, @"\[[^\[\]]+\]", "", RegexOptions.CultureInvariant);
        var answerWords = DetailedQueryPipeline.Words(answerWithoutCitations);
        double? groundedness = answerWords.Length == 0 || run.Context.Count == 0 ? null : answerWords.Count(evidenceWords.Contains) / (double)answerWords.Length;
        var abstentionCorrect = run.Abstained == question.ExpectedAbstention;
        var metrics = new LiveEvaluationMetrics(Recall(1), Recall(5), question.GoldAnchors.Count == 0 ? null : firstRank == 0 ? 0 : 1d / firstRank, question.GoldAnchors.Count == 0 ? null : firstCitation is not null && Relevant(firstCitation) ? 1 : 0, question.GoldAnchors.Count == 0 ? null : citationChunks.Length == 0 ? 0 : citationChunks.Count(Relevant) / (double)run.Citations.Count, groundedness, abstentionCorrect ? 1 : 0, question.ExpectedAnswer is null ? null : Normalize(answerWithoutCitations) == Normalize(question.ExpectedAnswer) ? 1 : 0, question.AnswerKeywords is not { Count: > 0 } ? null : question.AnswerKeywords.Count(keyword => Contains(answerWithoutCitations, keyword)) / (double)question.AnswerKeywords.Count);
        return new LiveEvaluationOutcome(question.Id, question.Question, question.ExpectedAnswer, question.ExpectedSourceFile, question.ExpectedAbstention, metrics, run);
    }
    public static LiveEvaluationMetrics Aggregate(IReadOnlyList<LiveEvaluationOutcome> outcomes)
    {
        double? Average(Func<LiveEvaluationMetrics, double?> metric)
        {
            var values = outcomes.Select(outcome => metric(outcome.Metrics)).OfType<double>().ToArray();
            return values.Length == 0 ? null : values.Average();
        }
        return new LiveEvaluationMetrics(Average(metric => metric.RecallAt1), Average(metric => metric.RecallAt5), Average(metric => metric.MrrAt5), Average(metric => metric.CitationAccuracy), Average(metric => metric.CitationPrecision), Average(metric => metric.Groundedness), Average(metric => metric.AbstentionAccuracy), Average(metric => metric.ExactAnswerAccuracy), Average(metric => metric.AnswerKeywordHitRate));
    }
    private static bool Contains(string text, string phrase) => Normalize(text).Contains(Normalize(phrase), StringComparison.Ordinal);
    private static string Normalize(string text) => Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
}
