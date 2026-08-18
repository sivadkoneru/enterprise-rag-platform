using Rag.Evals.Dataset;

namespace Rag.Evals.Scoring;

/// <summary>Everything one question produced under one chunking strategy.</summary>
internal sealed record QuestionOutcome(
    string QuestionId,
    QuestionType Type,
    Difficulty Difficulty,
    IReadOnlyList<string> RetrievedChunkIds,
    int AnchorCount,
    int AnchorsFoundAt1,
    int AnchorsFoundAt3,
    int AnchorsFoundAt5,
    int AnchorsFoundByUnionOnly,
    int FirstRelevantRank,
    bool TopCitationCorrect,
    double CitationPrecision,
    double Groundedness,
    IReadOnlyList<string> UngroundedTokens,
    bool AnswerKeywordsHit,
    double SupportZScore,
    bool Supported,
    int ContextChars)
{
    public bool FullyCovered => AnchorCount > 0 && AnchorsFoundAt5 == AnchorCount;

    public double RecallAt1 => Ratio(AnchorsFoundAt1);

    public double RecallAt3 => Ratio(AnchorsFoundAt3);

    public double RecallAt5 => Ratio(AnchorsFoundAt5);

    public double FragmentationRate => Ratio(AnchorsFoundByUnionOnly);

    public double ReciprocalRank => FirstRelevantRank <= 0 ? 0 : 1.0 / FirstRelevantRank;

    private double Ratio(int found) => AnchorCount == 0 ? 0 : (double)found / AnchorCount;
}

/// <summary>Aggregate metrics for one chunking strategy over the whole dataset.</summary>
internal sealed record StrategyReport(
    string Strategy,
    int ChunkCount,
    double AverageChunkChars,
    int P95ChunkChars,
    int IndexEmbedCalls,
    double Recall1,
    double Recall3,
    double Recall5,
    double FullCoverage5,
    double Mrr5,
    double CitationAccuracy1,
    double CitationPrecision5,
    double CitationIntegrity,
    double Groundedness,
    double AnswerKeywordHitRate,
    double AbstentionAccuracy,
    double FalseSupportRate,
    double FalseAbstainRate,
    double AnswerableMeanZ,
    double UnanswerableMeanZ,
    double FragmentedAnchorRate,
    double AverageContextChars,
    int AverageContextTokens,
    IReadOnlyDictionary<string, double> RecallByDifficulty,
    IReadOnlyDictionary<string, double> MrrByDifficulty,
    IReadOnlyList<QuestionOutcome> Outcomes);

/// <summary>
/// The reproducibility header. A chunking benchmark that does not print the chunk size it used is
/// not reproducible, so the profile travels with the results and into the published table.
/// </summary>
internal sealed record RunProfile(
    int ChunkSize,
    int ChunkOverlap,
    double SemanticDistanceThreshold,
    int TopK,
    int EmbeddingDimensions,
    double SupportZThreshold,
    string LlmProvider,
    IReadOnlyList<string> Corpus)
{
    public static RunProfile Default(IReadOnlyList<string> corpus) =>
        new(800, 120, 0.22, 5, 1536, 2.5, "deterministic", corpus);
}

internal sealed record EvalRun(
    int SchemaVersion,
    RunProfile Profile,
    int QuestionCount,
    IReadOnlyList<StrategyReport> Strategies);
