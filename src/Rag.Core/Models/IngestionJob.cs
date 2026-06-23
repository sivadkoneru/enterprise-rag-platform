namespace Rag.Core.Models;

public enum IngestionJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed
}

public sealed record IngestionJob(
    string Id,
    IngestionRequest Request,
    IngestionJobStatus Status,
    int DocumentCount = 0,
    int ChunkCount = 0,
    int TotalSourceCount = 0,
    int ProcessedSourceCount = 0,
    IReadOnlyList<string>? DocumentIds = null,
    IReadOnlyList<string>? ChunkIds = null,
    string? CurrentSource = null,
    string? WorkerId = null,
    string? Error = null,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset UpdatedAt = default,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null);

public sealed record IngestionProgress(
    int TotalSourceCount,
    int ProcessedSourceCount,
    int DocumentCount,
    int ChunkCount,
    IReadOnlyList<string> DocumentIds,
    IReadOnlyList<string> ChunkIds,
    string? CurrentSource,
    DateTimeOffset UpdatedAt);
