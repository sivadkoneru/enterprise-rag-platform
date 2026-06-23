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

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        IReadOnlyList<float> queryVector,
        int topK,
        VectorSearchFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var client = Client();
        var filterClauses = BuildFilter(filter);
        var payload = new
        {
            knn = BuildKnn(queryVector, topK, filterClauses),
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

    private static object BuildKnn(IReadOnlyList<float> queryVector, int topK, object[]? filter)
    {
        var k = Math.Max(1, topK);
        var candidates = Math.Max(10, topK * 10);
        return filter is { Length: > 0 }
            ? new
            {
                field = "vector",
                query_vector = queryVector,
                k,
                num_candidates = candidates,
                filter
            }
            : new
            {
                field = "vector",
                query_vector = queryVector,
                k,
                num_candidates = candidates
            };
    }

    private static object[]? BuildFilter(VectorSearchFilter? filter)
    {
        if (filter is null)
        {
            return null;
        }

        var filters = new List<object>();
        AddTerms(filters, "documentId", filter.DocumentIds);
        AddTerms(filters, "metadata.source.keyword", filter.Sources);
        AddTerms(filters, "metadata.origin.keyword", filter.Origins);
        AddTerms(filters, "metadata.fileType.keyword", NormalizeFileTypes(filter.FileTypes));
        return filters.Count == 0 ? null : filters.ToArray();
    }

    private static void AddTerms(ICollection<object> filters, string field, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        filters.Add(new
        {
            terms = new Dictionary<string, IReadOnlyList<string>>
            {
                [field] = values
            }
        });
    }

    private static IReadOnlyList<string>? NormalizeFileTypes(IReadOnlyList<string>? fileTypes)
    {
        return fileTypes?.Select(type => type.StartsWith('.') ? type : $".{type}").ToArray();
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
