using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Stores;

public sealed class CosmosDocumentStore(IOptions<DocumentStoreOptions> options) : IDocumentStore
{
    private CosmosClient? _client;
    private Container? _documents;
    private Container? _chunks;

    public async Task UpsertDocumentAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken).ConfigureAwait(false);
        await containers.Documents.UpsertItemAsync(new CosmosDocument(document.Id, document.Id, document), new PartitionKey(document.Id), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertChunksAsync(IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var chunk in chunks)
        {
            await containers.Chunks.UpsertItemAsync(new CosmosChunk(chunk.Id, chunk.DocumentId, chunk), new PartitionKey(chunk.DocumentId), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
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
        return chunkIds.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
    }

    private async Task<(Container Documents, Container Chunks)> GetContainersAsync(CancellationToken cancellationToken)
    {
        if (_documents is not null && _chunks is not null)
        {
            return (_documents, _chunks);
        }

        var config = options.Value;
        _client ??= CreateClient(config);
        var databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(config.DatabaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var database = databaseResponse.Database;
        var documents = await database.CreateContainerIfNotExistsAsync("documents", "/documentId", cancellationToken: cancellationToken).ConfigureAwait(false);
        var chunks = await database.CreateContainerIfNotExistsAsync(config.ContainerName, "/documentId", cancellationToken: cancellationToken).ConfigureAwait(false);
        _documents = documents.Container;
        _chunks = chunks.Container;
        return (_documents, _chunks);
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

    private sealed record CosmosDocument(string id, string documentId, ParsedDocument Document);

    private sealed record CosmosChunk(string id, string documentId, TextChunk Chunk);
}
