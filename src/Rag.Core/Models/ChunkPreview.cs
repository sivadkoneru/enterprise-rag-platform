namespace Rag.Core.Models;

public sealed record ChunkPreview(
    string Strategy,
    int ChunkCount,
    double AverageSize,
    int Overlap,
    string Sample);
