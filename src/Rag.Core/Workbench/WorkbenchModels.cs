using Rag.Core.Models;

namespace Rag.Core.Workbench;

public sealed record WorkbenchCorpus(string Id, string Name, string Description, DateTimeOffset CreatedAt);
public sealed record IndexProfile(string Id, string CorpusId, string Name, string Strategy, int ChunkSize, int ChunkOverlap, string EmbeddingModel, int EmbeddingDimensions, string Status, int DocumentCount, int ChunkCount, DateTimeOffset CreatedAt, IReadOnlyList<string> DocumentIds, IReadOnlyList<string> ChunkIds)
{
    public int EmbeddingOperations { get; init; }
    public string? VectorIndexName { get; init; }
    public double SemanticDistanceThreshold { get; init; } = 0.22;
}
public sealed record CreateCorpusRequest(string Name, string Description = "");
public sealed record CreateProfileRequest(string Name, string Strategy, int ChunkSize = 800, int ChunkOverlap = 120, string? EmbeddingModel = null, int EmbeddingDimensions = 1536);
public sealed record WorkbenchDocument(string Id, string ProfileId, string Filename, string Content, int ChunkCount, string Source, IReadOnlyList<string> ChunkIds);
public sealed record WorkbenchIngestionRequest(IReadOnlyList<string> Sources);
public sealed record WorkbenchJob(string Id, string Kind, string Status, string? ProfileId, int Completed, int Total, string? Error, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Payload);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Offset, int Limit);
public sealed record DetailedQueryRequest(string Question, string CorpusId, string ProfileId, int TopK = 5, string Mode = "vector", bool Reranker = false, double MinRelevance = 0, bool Neighbors = false, int MaxContextTokens = 4096);
public sealed record DetailedCandidate(string Id, string DocumentId, string Filename, int Index, string Content, double? VectorScore, double? LexicalScore, double? FusionScore, double? RerankerScore, int RankBefore, int RankAfter, bool InContext, string? ExclusionReason = null, bool IsNeighbor = false);
public sealed record DetailedCitation(int Number, string ChunkId, string DocumentId, bool Valid);
public sealed record DetailedStage(string Id, string Name, string Status, long DurationMs, string Detail, IReadOnlyDictionary<string, object?> Diagnostics);
public sealed record DetailedRun(string Id, DateTimeOffset CreatedAt, string Question, string CorpusId, string ProfileId, string Answer, IReadOnlyList<DetailedCitation> Citations, IReadOnlyList<DetailedCandidate> Candidates, IReadOnlyList<DetailedCandidate> Context, IReadOnlyList<DetailedStage> Trace, long TotalLatencyMs, int ContextTokens, int OutputTokens, string TokenUsageKind, bool Abstained, IReadOnlyList<string> InvalidCitations)
{
    public string? Provider { get; init; }
    public string? EmbeddingModel { get; init; }
    public string? ChatModel { get; init; }
    public string? SystemPromptHash { get; init; }
    public string? TraceId { get; init; }
    public int? EmbeddingTokens { get; init; }
    public int EmbeddingCalls { get; init; }
    public int? PromptTokens { get; init; }
    public int? TotalTokens { get; init; }
}
public sealed record EvaluationAnchor(string Phrase, string? Section = null);
public sealed record LiveEvaluationQuestion(string Id, string Question, string? ExpectedSourceFile, IReadOnlyList<EvaluationAnchor> GoldAnchors, bool ExpectedAbstention = false, string? ExpectedAnswer = null, IReadOnlyList<string>? AnswerKeywords = null, string Type = "custom", string Difficulty = "custom");
public sealed record LiveEvaluationRequest(IReadOnlyList<string> ProfileIds, IReadOnlyList<LiveEvaluationQuestion> Questions, int TopK = 5, string Mode = "vector", bool Reranker = false, double MinRelevance = 0.7, bool Neighbors = false, int MaxContextTokens = 4096);
public sealed record LiveEvaluationMetrics(double? RecallAt1, double? RecallAt5, double? MrrAt5, double? CitationAccuracy, double? CitationPrecision, double? Groundedness, double? AbstentionAccuracy, double? ExactAnswerAccuracy = null, double? AnswerKeywordHitRate = null);
public sealed record LiveEvaluationOutcome(string QuestionId, string Question, string? ExpectedAnswer, string? ExpectedSourceFile, bool ExpectedAbstention, LiveEvaluationMetrics Metrics, DetailedRun Run);
public sealed record LiveProfileEvaluation(string ProfileId, string ProfileName, LiveEvaluationMetrics Metrics, int EmbeddingOperations, double AverageContextTokens, double AverageLatencyMs, IReadOnlyList<LiveEvaluationOutcome> Outcomes)
{
    public IndexProfile? ProfileSnapshot { get; init; }
    public string? SourceRevision { get; init; }
}
public sealed record LiveEvaluationReport(string Id, DateTimeOffset CreatedAt, string Status, IReadOnlyList<LiveEvaluationQuestion> Questions, IReadOnlyList<LiveProfileEvaluation> Profiles, string GroundednessCaveat, int TopK)
{
    public int SchemaVersion { get; init; } = 2;
    public LiveEvaluationRequest? QuerySettings { get; init; }
}
public sealed class RerankerOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}
public interface IWorkbenchStateStore
{
    Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default);
}
public interface IRerankerClient
{
    Task<IReadOnlyDictionary<int, double>> RerankAsync(string question, IReadOnlyList<string> documents, CancellationToken cancellationToken = default);
}

public sealed class WorkbenchQueryOptions
{
    public int TopK { get; set; } = 5;
    public string Mode { get; set; } = "vector";
    public bool Reranker { get; set; }
    public double MinRelevance { get; set; } = 0.7;
    public bool Neighbors { get; set; }
    public int MaxContextTokens { get; set; } = 4096;
}

public interface IWorkbenchJobStateStore : IWorkbenchStateStore;
