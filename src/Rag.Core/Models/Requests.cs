namespace Rag.Core.Models;

public sealed record IngestionRequest(string Path, string? Strategy = null);

public sealed record IngestionResult(
    string DocumentId,
    int ChunkCount,
    string Strategy,
    IReadOnlyList<string> ChunkIds);

public sealed record QueryRequest(string Question, int TopK = 5);

public sealed record ChatMessage(string Role, string Content);
