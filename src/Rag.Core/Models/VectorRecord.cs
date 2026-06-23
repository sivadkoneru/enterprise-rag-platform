namespace Rag.Core.Models;

public sealed record VectorRecord(
    string ChunkId,
    string DocumentId,
    IReadOnlyList<float> Vector,
    IReadOnlyDictionary<string, string> Metadata);
