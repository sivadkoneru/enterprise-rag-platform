namespace Rag.Core.Models;

/// <summary>
/// The outcome of an ingestion run. Documents are reported as a set: a run ingests every source it
/// is given, and under parallel ingestion no single document is meaningfully "the" result.
/// </summary>
public sealed record IngestionResult(
    int ChunkCount,
    string Strategy,
    IReadOnlyList<string> ChunkIds,
    IReadOnlyList<string> DocumentIds);
