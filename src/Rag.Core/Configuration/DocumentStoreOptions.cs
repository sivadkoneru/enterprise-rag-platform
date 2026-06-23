namespace Rag.Core.Configuration;

public sealed class DocumentStoreOptions
{
    public string Provider { get; set; } = "memory";

    public string? ConnectionString { get; set; }

    public string? Endpoint { get; set; }

    public string? Key { get; set; }

    public string DatabaseName { get; set; } = "rag";

    public string ContainerName { get; set; } = "chunks";

    public string DocumentsContainerName { get; set; } = "documents";

    public string LocalPath { get; set; } = ".rag/document-store";
}
