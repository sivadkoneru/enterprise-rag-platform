using Rag.Core.Workbench;

namespace Rag.LiveEvals;

public sealed record ChunkSpan(string Source, int Start, int End);
public sealed record EvidenceAnchor(string Source, int Start, int End, string Text);
public sealed record EvaluationCase(string Id, string Split, string Category, string Question, bool Answerable, IReadOnlyList<EvidenceAnchor> Anchors, IReadOnlyList<string> ReferenceFacts);
public sealed record Dataset(int SchemaVersion, string Version, string Status, IReadOnlyDictionary<string, string> DocumentHashes, IReadOnlyList<EvaluationCase> Cases);
public sealed record Pricing(string Date, string Currency, decimal EmbeddingPerMillion, decimal InputPerMillion, decimal OutputPerMillion, decimal JudgeInputPerMillion, decimal JudgeOutputPerMillion);
public sealed record RunConfiguration(int SchemaVersion, string ApiUrl, string CorpusId, string ProfileId, string EmbeddingModel, string ChatModel, string Commit, string Split, decimal BudgetUsd, decimal IngestionEstimatedUsd, Pricing Pricing, string? JudgeEndpoint = null, string? JudgeModel = null, double MinRelevance = 0.7, int MaxContextTokens = 4096);
public sealed record RetrievalMetrics(double? Recall, double? Mrr, double? Ndcg, double? FullCoverage, string? MissingReason, double? FragmentedEvidence = null);
public sealed record SemanticJudgment(int Claims, int SupportedClaims, int CorrectClaims, int RequiredFacts, int CoveredFacts, int Contradictions, int Citations, int SupportedCitations, int CitedClaims, bool SubstantiveAnswer, string Explanation);
public sealed record CaseResult(string Id, string Mode, string Status, double DurationMs, DetailedRun? Run, IReadOnlyDictionary<int, RetrievalMetrics>? Retrieval, SemanticJudgment? Judge, string? JudgeFailure, decimal ReservedUsd, decimal? QueryEstimatedUsd, decimal? JudgeEstimatedUsd, string? Failure);

/// <summary>Reservations are never released: retries, missing usage and failures remain charged conservatively.</summary>
public sealed class RunBudget(decimal maximum, decimal ingestion)
{
    public decimal Reserved { get; private set; } = ingestion >= 0 && ingestion <= maximum && maximum is > 0 and <= 10
        ? ingestion : throw new ArgumentException("Budget must be at most $10 and include a nonnegative indexing estimate.");
    public bool TryReserve(decimal cost)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cost);
        if (Reserved + cost > maximum)
        {
            return false;
        }

        Reserved += cost;
        return true;
    }
}
