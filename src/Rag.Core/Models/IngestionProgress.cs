namespace Rag.Core.Models;

public sealed record IngestionProgress(
    int TotalSourceCount,
    int ProcessedSourceCount,
    int DocumentCount,
    int ChunkCount,
    IReadOnlyList<string> DocumentIds,
    IReadOnlyList<string> ChunkIds,
    string? CurrentSource,
    DateTimeOffset UpdatedAt);
