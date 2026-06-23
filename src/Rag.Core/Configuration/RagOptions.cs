namespace Rag.Core.Configuration;

public sealed class RagOptions
{
    public string ChunkingStrategy { get; set; } = "fixed";
}

public sealed class ChunkingOptions
{
    public int Size { get; set; } = 800;

    public int Overlap { get; set; } = 120;

    public int SemanticWindowSize { get; set; } = 5;

    public double SemanticDistanceThreshold { get; set; } = 0.22;

    public bool SemanticRefineWithChat { get; set; }
}

public sealed class LlmOptions
{
    public string Provider { get; set; } = "deterministic";

    public string? ApiKey { get; set; }

    public string? EmbeddingEndpoint { get; set; }

    public string? EmbeddingModel { get; set; }

    public int EmbeddingDimensions { get; set; } = 1536;

    public string? ChatEndpoint { get; set; }

    public string? ChatModel { get; set; }
}

public sealed class DocumentStoreOptions
{
    public string Provider { get; set; } = "memory";

    public string? ConnectionString { get; set; }

    public string? Endpoint { get; set; }

    public string? Key { get; set; }

    public string DatabaseName { get; set; } = "rag";

    public string ContainerName { get; set; } = "chunks";

    public string LocalPath { get; set; } = ".rag/document-store";
}

public sealed class VectorStoreOptions
{
    public string Provider { get; set; } = "memory";

    public string? Endpoint { get; set; }

    public string IndexName { get; set; } = "rag-chunks";

    public int Dimensions { get; set; } = 1536;

    public string LocalPath { get; set; } = ".rag/vector-store";
}
