namespace Rag.Core.Configuration;

public sealed class VectorStoreOptions
{
    public string Provider { get; set; } = "memory";

    public string? Endpoint { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string IndexName { get; set; } = "rag-chunks";

    public int Dimensions { get; set; } = 1536;
}
