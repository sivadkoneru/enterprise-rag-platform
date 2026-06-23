namespace Rag.Core.Configuration;

public sealed class ChunkingOptions
{
    public int Size { get; set; } = 800;

    public int Overlap { get; set; } = 120;

    public double SemanticDistanceThreshold { get; set; } = 0.22;
}
