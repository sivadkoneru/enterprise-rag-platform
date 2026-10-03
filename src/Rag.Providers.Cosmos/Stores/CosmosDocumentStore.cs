using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Stores;

public sealed class CosmosDocumentStore(IOptions<DocumentStoreOptions> options, ILogger<CosmosDocumentStore> logger) : IDocumentStore, IChunkDeletionStore, IDisposable
{
    public async Task DeleteChunksAsync(string documentId, IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var id in chunkIds)
        {
            try { await containers.Chunks.DeleteItemAsync<CosmosChunk>(id, new PartitionKey(documentId), cancellationToken: cancellationToken).ConfigureAwait(false); }
            catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound) { /* Idempotent cleanup after an interrupted ingestion. */ }
        }
    }

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private CosmosClient? _client;
    private Container? _documents;
    private Container? _chunks;

    public async Task UpsertDocumentAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken).ConfigureAwait(false);
        await containers.Documents.UpsertItemAsync(new CosmosDocument(document.Id, document.Id, document), new PartitionKey(document.Id), cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Upserted 1 document to the Cosmos DB document store.");
    }

    public async Task UpsertChunksAsync(IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var chunk in chunks)
        {
            await containers.Chunks.UpsertItemAsync(new CosmosChunk(chunk.Id, chunk.DocumentId, chunk), new PartitionKey(chunk.DocumentId), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        logger.LogDebug("Upserted {ChunkCount} chunk(s) to the Cosmos DB document store.", chunks.Count);
    }

    public async Task<IReadOnlyList<TextChunk>> GetChunksAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken).ConfigureAwait(false);
        var query = new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id)")
            .WithParameter("@ids", chunkIds);
        var iterator = containers.Chunks.GetItemQueryIterator<CosmosChunk>(query);
        var chunks = new List<TextChunk>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            chunks.AddRange(page.Select(item => item.Chunk));
        }

        var byId = chunks.ToDictionary(chunk => chunk.Id, StringComparer.Ordinal);
        var result = chunkIds.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
        if (result.Length < chunkIds.Count)
        {
            logger.LogWarning(
                "Requested {RequestedCount} chunk(s) but found {FoundCount} in the Cosmos DB document store.",
                chunkIds.Count,
                result.Length);
        }

        return result;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _initLock.Dispose();
    }

    private async Task<(Container Documents, Container Chunks)> GetContainersAsync(CancellationToken cancellationToken)
    {
        if (_documents is not null && _chunks is not null)
        {
            return (_documents, _chunks);
        }

        // Parallel ingestion reaches this concurrently; without the gate each caller would
        // create and leak its own CosmosClient.
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_documents is not null && _chunks is not null)
            {
                return (_documents, _chunks);
            }

            var config = options.Value;
            _client ??= CreateClient(config);
            var databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(config.DatabaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
            var database = databaseResponse.Database;
            var documents = await database.CreateContainerIfNotExistsAsync(config.DocumentsContainerName, "/documentId", cancellationToken: cancellationToken).ConfigureAwait(false);
            var chunks = await database.CreateContainerIfNotExistsAsync(config.ContainerName, "/documentId", cancellationToken: cancellationToken).ConfigureAwait(false);
            _documents = documents.Container;
            _chunks = chunks.Container;
            return (_documents, _chunks);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static CosmosClient CreateClient(DocumentStoreOptions config)
    {
        if (!string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            return new CosmosClient(config.ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(config.Endpoint) && !string.IsNullOrWhiteSpace(config.Key))
        {
            return new CosmosClient(config.Endpoint, config.Key);
        }

        throw new InvalidOperationException("COSMOS_CONNECTION_STRING or COSMOS_ENDPOINT/COSMOS_KEY is required for Cosmos DB.");
    }

    // Microsoft.Azure.Cosmos v3 serializes items with Newtonsoft.Json by default. The explicit
    // JsonProperty attributes drive the wire format ("id" and "documentId") independently of the
    // PascalCase member names, so the required "id" field and the "/documentId" partition key path
    // (see CreateContainerIfNotExistsAsync below) keep serializing exactly as before.
    private sealed record CosmosDocument(
        [property: Newtonsoft.Json.JsonProperty("id")] string Id,
        [property: Newtonsoft.Json.JsonProperty("documentId")] string DocumentId,
        ParsedDocument Document);

    private sealed record CosmosChunk(
        [property: Newtonsoft.Json.JsonProperty("id")] string Id,
        [property: Newtonsoft.Json.JsonProperty("documentId")] string DocumentId,
        TextChunk Chunk);
}
