namespace Rag.Core.Models;

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
