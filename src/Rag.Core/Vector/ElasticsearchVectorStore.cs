using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Vector;

public sealed class ElasticsearchVectorStore(IHttpClientFactory httpClientFactory, IOptions<VectorStoreOptions> options) : IVectorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        var client = Client();
        var response = await client.GetAsync(IndexPath(), cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var mapping = new
        {
            mappings = new
            {
                properties = new
                {
                    chunkId = new { type = "keyword" },
                    documentId = new { type = "keyword" },
                    metadata = new { type = "object", enabled = true },
                    vector = new { type = "dense_vector", dims = options.Value.Dimensions, index = true, similarity = "cosine" }
                }
            }
        };

        using var createResponse = await client.PutAsJsonAsync(IndexPath(), mapping, JsonOptions, cancellationToken).ConfigureAwait(false);
        createResponse.EnsureSuccessStatusCode();
    }

    public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        var client = Client();
        foreach (var record in records)
        {
            var payload = new
            {
                record.ChunkId,
                record.DocumentId,
                record.Metadata,
                vector = record.Vector
            };
            using var response = await client.PutAsJsonAsync($"{IndexPath()}/_doc/{Uri.EscapeDataString(record.ChunkId)}", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        using var refresh = await client.PostAsync($"{IndexPath()}/_refresh", null, cancellationToken).ConfigureAwait(false);
        refresh.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(IReadOnlyList<float> queryVector, int topK, CancellationToken cancellationToken = default)
    {
        var client = Client();
        var payload = new
        {
            knn = new
            {
                field = "vector",
                query_vector = queryVector,
                k = Math.Max(1, topK),
                num_candidates = Math.Max(10, topK * 10)
            },
            _source = new[] { "chunkId", "documentId" }
        };

        using var response = await client.PostAsJsonAsync($"{IndexPath()}/_search", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var hits = document.RootElement.GetProperty("hits").GetProperty("hits");
        var results = new List<VectorSearchResult>();
        foreach (var hit in hits.EnumerateArray())
        {
            var source = hit.GetProperty("_source");
            results.Add(new VectorSearchResult(
                source.GetProperty("chunkId").GetString() ?? string.Empty,
                source.GetProperty("documentId").GetString() ?? string.Empty,
                hit.GetProperty("_score").GetDouble()));
        }

        return results;
    }

    private HttpClient Client()
    {
        if (string.IsNullOrWhiteSpace(options.Value.Endpoint))
        {
            throw new InvalidOperationException("VectorStore:Endpoint or ELASTICSEARCH_URI is required for Elasticsearch.");
        }

        var client = httpClientFactory.CreateClient("rag-elasticsearch");
        client.BaseAddress ??= new Uri(options.Value.Endpoint, UriKind.Absolute);
        return client;
    }

    private string IndexPath()
    {
        return $"/{options.Value.IndexName}";
    }
}
