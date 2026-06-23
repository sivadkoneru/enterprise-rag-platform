namespace Rag.Core.Models;

public sealed record SourceCitation(
    string DocumentId,
    string ChunkId,
    string Source,
    int ChunkIndex,
    double Score);
