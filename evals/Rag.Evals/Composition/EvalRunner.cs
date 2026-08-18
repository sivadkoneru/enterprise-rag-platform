using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Evals.Dataset;
using Rag.Evals.Scoring;

namespace Rag.Evals.Composition;

/// <summary>Ingests the corpus under each strategy and scores the whole dataset against it.</summary>
internal static class EvalRunner
{
    public static readonly IReadOnlyList<string> Strategies = ["fixed", "recursive", "markdown-aware", "semantic"];

    public static async Task<EvalRun> RunAsync(
        RunProfile profile,
        IReadOnlyList<string> corpusPaths,
        IReadOnlyList<ResolvedQuestion> questions,
        CancellationToken cancellationToken = default)
    {
        var reports = new List<StrategyReport>(Strategies.Count);
        foreach (var strategy in Strategies)
        {
            reports.Add(await RunStrategyAsync(strategy, profile, corpusPaths, questions, cancellationToken).ConfigureAwait(false));
        }

        return new EvalRun(1, profile, questions.Count, reports);
    }

    private static async Task<StrategyReport> RunStrategyAsync(
        string strategy,
        RunProfile profile,
        IReadOnlyList<string> corpusPaths,
        IReadOnlyList<ResolvedQuestion> questions,
        CancellationToken cancellationToken)
    {
        using var host = EvalHost.Build(strategy, profile);

        var ingestion = await host.Get<IIngestionPipeline>()
            .IngestAsync(new IngestionRequest(Sources: corpusPaths), cancellationToken)
            .ConfigureAwait(false);

        var documentStore = host.Get<IDocumentStore>();
        var vectorStore = host.Get<IVectorStore>();
        var embeddingClient = host.Get<IEmbeddingClient>();
        var queryPipeline = host.Get<IQueryPipeline>();

        var allChunks = await documentStore.GetChunksAsync(ingestion.ChunkIds, cancellationToken).ConfigureAwait(false);
        var chunkSizes = allChunks.Select(chunk => chunk.Text.Length).ToArray();

        // Index cost is measured over ingestion only; the query-side embeds below are reported
        // through context size instead, so the two never blur into one number.
        var indexEmbedCalls = host.Counter.Calls;

        var outcomes = new List<QuestionOutcome>(questions.Count);
        var integrityFailures = 0;

        foreach (var resolved in questions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var question = resolved.Question;
            var answer = await queryPipeline
                .QueryAsync(new QueryRequest(question.Question, profile.TopK), cancellationToken)
                .ConfigureAwait(false);

            var retrieved = await HydrateAsync(documentStore, answer.Citations, cancellationToken).ConfigureAwait(false);
            integrityFailures += CountIntegrityFailures(answer.Citations, retrieved);

            int FoundWithin(int k)
            {
                var top = retrieved.Take(k).ToArray();
                return resolved.Anchors.Count(anchor => Scorers.Found(top, anchor));
            }

            var fragmented = resolved.Anchors.Count(anchor =>
                !retrieved.Any(chunk => Scorers.Covers(chunk, anchor)) && Scorers.UnionCovers(retrieved, anchor));

            var (groundedness, ungrounded) = Scorers.Groundedness(answer.Answer, retrieved);

            var topCitationCorrect = retrieved.Count > 0
                && question.ExpectedSourceFile is not null
                && string.Equals(
                    Path.GetFileName(answer.Citations[0].Source),
                    question.ExpectedSourceFile,
                    StringComparison.Ordinal)
                && resolved.Anchors.Any(anchor => Scorers.Covers(retrieved[0], anchor));

            var relevantRetrieved = retrieved.Count(chunk => resolved.Anchors.Any(anchor => Scorers.Covers(chunk, anchor)));

            var zScore = await SupportZScoreAsync(
                embeddingClient, vectorStore, question.Question, allChunks.Count, cancellationToken).ConfigureAwait(false);

            outcomes.Add(new QuestionOutcome(
                question.Id,
                question.Type,
                question.Difficulty,
                [.. retrieved.Select(chunk => chunk.Id)],
                resolved.Anchors.Count,
                FoundWithin(1),
                FoundWithin(3),
                FoundWithin(profile.TopK),
                fragmented,
                Scorers.FirstRelevantRank(retrieved, resolved.Anchors),
                topCitationCorrect,
                retrieved.Count == 0 ? 0 : (double)relevantRetrieved / retrieved.Count,
                groundedness,
                ungrounded,
                Scorers.AnswerKeywordsHit(answer.Answer, question.AnswerKeywords),
                zScore,
                zScore >= profile.SupportZThreshold,
                retrieved.Sum(chunk => chunk.Text.Length)));
        }

        return Aggregate(strategy, allChunks.Count, chunkSizes, indexEmbedCalls, integrityFailures, outcomes, profile);
    }

    /// <summary>Re-orders hydrated chunks to match citation order, which is descending by score.</summary>
    private static async Task<IReadOnlyList<TextChunk>> HydrateAsync(
        IDocumentStore documentStore,
        IReadOnlyList<SourceCitation> citations,
        CancellationToken cancellationToken)
    {
        if (citations.Count == 0)
        {
            return [];
        }

        var chunks = await documentStore
            .GetChunksAsync([.. citations.Select(citation => citation.ChunkId)], cancellationToken)
            .ConfigureAwait(false);
        var byId = chunks.ToDictionary(chunk => chunk.Id, StringComparer.Ordinal);

        return [.. citations
            .Where(citation => byId.ContainsKey(citation.ChunkId))
            .Select(citation => byId[citation.ChunkId])];
    }

    /// <summary>
    /// Citations must describe the chunks the prompt actually contained. A citation that does not
    /// hydrate, or whose index or document disagrees with its chunk, means the answer points at
    /// something the model never saw — the exact failure a citation metric exists to catch.
    /// </summary>
    private static int CountIntegrityFailures(IReadOnlyList<SourceCitation> citations, IReadOnlyList<TextChunk> retrieved)
    {
        if (citations.Count != retrieved.Count)
        {
            return Math.Abs(citations.Count - retrieved.Count);
        }

        var failures = 0;
        for (var index = 0; index < citations.Count; index++)
        {
            var citation = citations[index];
            var chunk = retrieved[index];
            if (!string.Equals(citation.ChunkId, chunk.Id, StringComparison.Ordinal) ||
                citation.ChunkIndex != chunk.Index ||
                !string.Equals(citation.DocumentId, chunk.DocumentId, StringComparison.Ordinal))
            {
                failures++;
            }
        }

        return failures;
    }

    /// <summary>
    /// Scores the whole index for one query so abstention can be judged on separability. The extra
    /// search is affordable because the corpus is small and the harness owns the container.
    /// </summary>
    private static async Task<double> SupportZScoreAsync(
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        string question,
        int chunkCount,
        CancellationToken cancellationToken)
    {
        if (chunkCount < 2)
        {
            return 0;
        }

        var vector = await embeddingClient.EmbedAsync(question, cancellationToken).ConfigureAwait(false);
        var all = await vectorStore.SearchAsync(vector, chunkCount, null, cancellationToken).ConfigureAwait(false);
        return Scorers.SupportZScore([.. all.Select(match => match.Score)]);
    }

    private static StrategyReport Aggregate(
        string strategy,
        int chunkCount,
        IReadOnlyList<int> chunkSizes,
        int indexEmbedCalls,
        int integrityFailures,
        IReadOnlyList<QuestionOutcome> outcomes,
        RunProfile profile)
    {
        var answerable = outcomes.Where(outcome => outcome.Type != QuestionType.Unanswerable).ToArray();
        var unanswerable = outcomes.Where(outcome => outcome.Type == QuestionType.Unanswerable).ToArray();

        static double Mean(IEnumerable<double> values)
        {
            var array = values.ToArray();
            return array.Length == 0 ? 0 : array.Average();
        }

        static double Round(double value) => Math.Round(value, 4);

        IReadOnlyDictionary<string, double> ByDifficulty(Func<QuestionOutcome, double> selector) =>
            answerable
                .GroupBy(outcome => outcome.Difficulty.ToString().ToLowerInvariant())
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => Round(group.Average(selector)), StringComparer.Ordinal);

        var totalCitations = outcomes.Sum(outcome => outcome.RetrievedChunkIds.Count);
        var averageContextChars = Mean(outcomes.Select(outcome => (double)outcome.ContextChars));
        var abstentionCorrect = unanswerable.Count(outcome => !outcome.Supported)
            + answerable.Count(outcome => outcome.Supported);

        return new StrategyReport(
            strategy,
            chunkCount,
            Math.Round(chunkSizes.Count == 0 ? 0 : chunkSizes.Average(), 1),
            Scorers.Percentile(chunkSizes, 95),
            indexEmbedCalls,
            Round(Mean(answerable.Select(outcome => outcome.RecallAt1))),
            Round(Mean(answerable.Select(outcome => outcome.RecallAt3))),
            Round(Mean(answerable.Select(outcome => outcome.RecallAt5))),
            Round(Mean(answerable.Select(outcome => outcome.FullyCovered ? 1.0 : 0))),
            Round(Mean(answerable.Select(outcome => outcome.ReciprocalRank))),
            Round(Mean(answerable.Select(outcome => outcome.TopCitationCorrect ? 1.0 : 0))),
            Round(Mean(answerable.Select(outcome => outcome.CitationPrecision))),
            totalCitations == 0 ? 1 : Round(1 - ((double)integrityFailures / totalCitations)),
            Round(Mean(outcomes.Select(outcome => outcome.Groundedness))),
            Round(Mean(answerable.Select(outcome => outcome.AnswerKeywordsHit ? 1.0 : 0))),
            Round(outcomes.Count == 0 ? 0 : (double)abstentionCorrect / outcomes.Count),
            Round(unanswerable.Length == 0 ? 0 : (double)unanswerable.Count(outcome => outcome.Supported) / unanswerable.Length),
            Round(answerable.Length == 0 ? 0 : (double)answerable.Count(outcome => !outcome.Supported) / answerable.Length),
            Round(Mean(answerable.Select(outcome => outcome.SupportZScore))),
            Round(Mean(unanswerable.Select(outcome => outcome.SupportZScore))),
            Round(Mean(answerable.Select(outcome => outcome.FragmentationRate))),
            Math.Round(averageContextChars, 1),
            Scorers.EstimateTokens((int)Math.Round(averageContextChars)),
            ByDifficulty(outcome => outcome.RecallAt5),
            ByDifficulty(outcome => outcome.ReciprocalRank),
            outcomes);
    }
}
