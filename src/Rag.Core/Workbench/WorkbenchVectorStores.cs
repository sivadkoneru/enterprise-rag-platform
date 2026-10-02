using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Vector;

namespace Rag.Core.Workbench;

/// <summary>Live profiles have their own Elasticsearch indices; the legacy index stays unchanged.</summary>
public sealed class WorkbenchVectorStores(IVectorStore fallback, IHttpClientFactory clients, IOptions<VectorStoreOptions> options, ILoggerFactory loggers)
{
    public IVectorStore ForProfile(IndexProfile profile)
    {
        var config = options.Value;
        if (!config.Provider.Equals("elasticsearch", StringComparison.OrdinalIgnoreCase)) { return fallback; }
        var scoped = new VectorStoreOptions
        {
            Provider = config.Provider, Endpoint = config.Endpoint, Username = config.Username, Password = config.Password,
            Dimensions = profile.EmbeddingDimensions,
            IndexName = string.IsNullOrWhiteSpace(profile.VectorIndexName) ? $"{config.IndexName}-{profile.Id}" : profile.VectorIndexName
        };
        return new ElasticsearchVectorStore(clients, Options.Create(scoped), loggers.CreateLogger<ElasticsearchVectorStore>());
    }
}
