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
    IReadOnlyList<string>? DocumentIds = null,
    IReadOnlyList<string>? ChunkIds = null,
    string? Error = null,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null);
