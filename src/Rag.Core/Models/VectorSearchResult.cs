namespace Rag.Core.Models;

public sealed record VectorSearchResult(
    string ChunkId,
    string DocumentId,
    double Score);
