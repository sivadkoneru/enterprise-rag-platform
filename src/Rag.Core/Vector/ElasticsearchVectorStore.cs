using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Common;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Workbench;

namespace Rag.Core.Vector;

public sealed class ElasticsearchVectorStore(
    IHttpClientFactory httpClientFactory,
    IOptions<VectorStoreOptions> options,
    ILogger<ElasticsearchVectorStore> logger) : IVectorStore, ILexicalSearchStore, IChunkDeletionStore
{
    public async Task EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        var client = Client();
        using var existing = await client.GetAsync(IndexPath(), cancellationToken).ConfigureAwait(false);
        if (existing.IsSuccessStatusCode)
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
                    text = new { type = "text" },
                    metadata = new { type = "object", enabled = true },
                    vector = new { type = "dense_vector", dims = options.Value.Dimensions, index = true, similarity = "cosine" }
                }
            }
        };

        using var createResponse = await client.PutAsJsonAsync(IndexPath(), mapping, RagJson.Options, cancellationToken).ConfigureAwait(false);
        if (createResponse.IsSuccessStatusCode)
        {
            logger.LogInformation("Created Elasticsearch index {IndexName}.", options.Value.IndexName);
            return;
        }

        // Concurrent ingestion jobs can create the index between the probe and this request.
        var error = await createResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (createResponse.StatusCode == HttpStatusCode.BadRequest &&
            error.Contains("resource_already_exists_exception", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Lost the create race for Elasticsearch index {IndexName}; it already exists.",
                options.Value.IndexName);
            return;
        }

        throw new InvalidOperationException(
            $"Elasticsearch index '{options.Value.IndexName}' could not be created ({(int)createResponse.StatusCode}): {error}");
    }

    public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        logger.LogDebug("Bulk indexing {RecordCount} record(s) into Elasticsearch.", records.Count);
        var client = Client();
        var body = new StringBuilder();
        foreach (var record in records)
        {
            body.Append("{\"index\":{\"_id\":")
                .Append(JsonSerializer.Serialize(record.ChunkId, RagJson.Options))
                .Append("}}\n");
            body.Append(JsonSerializer.Serialize(
                new
                {
                    record.ChunkId,
                    record.DocumentId,
                    record.Metadata,
                    text = record.Metadata.GetValueOrDefault("text") ?? string.Empty,
                    vector = record.Vector
                },
                RagJson.Options)).Append('\n');
        }

        using var content = new StringContent(body.ToString(), Encoding.UTF8, "application/x-ndjson");
        using var response = await client.PostAsync($"{IndexPath()}/_bulk?refresh=true", content, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Elasticsearch bulk indexing failed ({(int)response.StatusCode}): {payload}");
        }

        ThrowOnItemErrors(payload);
    }

    private static void ThrowOnItemErrors(string bulkResponse)
    {
        using var document = JsonDocument.Parse(bulkResponse);
        if (!document.RootElement.TryGetProperty("errors", out var errors) || !errors.GetBoolean())
        {
            return;
        }

        var reason = document.RootElement.TryGetProperty("items", out var items)
            ? items.EnumerateArray()
                .Select(item => item.EnumerateObject().FirstOrDefault().Value)
                .Where(action => action.ValueKind == JsonValueKind.Object && action.TryGetProperty("error", out _))
                .Select(action => action.GetProperty("error").ToString())
                .FirstOrDefault()
            : null;

        throw new InvalidOperationException($"Elasticsearch rejected one or more chunks during bulk indexing: {reason ?? bulkResponse}");
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
            size = Math.Clamp(topK, 1, 400),
            knn = BuildKnn(queryVector, topK, filterClauses),
            _source = new[] { "chunkId", "documentId" }
        };

        using var response = await client.PostAsJsonAsync($"{IndexPath()}/_search", payload, RagJson.Options, cancellationToken).ConfigureAwait(false);
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

    public async Task DeleteChunksAsync(string documentId, IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        if (chunkIds.Count == 0) { return; }
        var payload = new { query = new { @bool = new { filter = new object[] {
            new { term = new { documentId } },
            new { terms = new { chunkId = chunkIds } }
        } } } };
        using var response = await Client().PostAsJsonAsync($"{IndexPath()}/_delete_by_query?refresh=true", payload, RagJson.Options, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (result.RootElement.TryGetProperty("failures", out var failures) && failures.GetArrayLength() > 0 ||
            result.RootElement.TryGetProperty("timed_out", out var timedOut) && timedOut.GetBoolean())
        {
            throw new InvalidOperationException("Obsolete vector cleanup failed; retry ingestion before querying this profile.");
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchLexicalAsync(string question, int topK, VectorSearchFilter filter, CancellationToken cancellationToken = default)
    {
        var clauses = BuildFilter(filter) ?? [];
        var payload = new
        {
            size = Math.Clamp(topK, 1, 400),
            query = new { @bool = new { must = new[] { new { match = new { text = question } } }, filter = clauses } },
            _source = new[] { "chunkId", "documentId" }
        };
        using var response = await Client().PostAsJsonAsync($"{IndexPath()}/_search", payload, RagJson.Options, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.GetProperty("hits").GetProperty("hits").EnumerateArray().Select(hit => new VectorSearchResult(hit.GetProperty("_source").GetProperty("chunkId").GetString() ?? "", hit.GetProperty("_source").GetProperty("documentId").GetString() ?? "", hit.GetProperty("_score").GetDouble())).ToArray();
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
        AddTerms(filters, "metadata.fileType.keyword", FileTypes.NormalizeAll(filter.FileTypes));
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

    private HttpClient Client()
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            throw new InvalidOperationException("VectorStore:Endpoint or ELASTICSEARCH_URI is required for Elasticsearch.");
        }

        var client = httpClientFactory.CreateClient("rag-elasticsearch");
        client.BaseAddress ??= new Uri(config.Endpoint, UriKind.Absolute);
        if (!string.IsNullOrWhiteSpace(config.Username))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }

    private string IndexPath()
    {
        return $"/{options.Value.IndexName}";
    }
}
