namespace Rag.Core.Configuration;

public sealed class JobStoreOptions
{
    public string Provider { get; set; } = "memory";

    public string? ConnectionString { get; set; }

    public string DatabaseName { get; set; } = "rag";

    public string CollectionName { get; set; } = "ingestion_jobs";
}
