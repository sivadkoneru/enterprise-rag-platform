using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Stores;

public sealed class MongoDocumentStore : IDocumentStore
{
    private readonly IMongoCollection<ParsedDocument> _documents;
    private readonly IMongoCollection<TextChunk> _chunks;

    public MongoDocumentStore(IOptions<DocumentStoreOptions> options)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            throw new InvalidOperationException("DocumentStore:ConnectionString or MONGO_CONNECTION_STRING is required for MongoDB.");
        }

        var client = new MongoClient(config.ConnectionString);
        var database = client.GetDatabase(config.DatabaseName);
        _documents = database.GetCollection<ParsedDocument>("documents");
        _chunks = database.GetCollection<TextChunk>(config.ContainerName);
    }

    public Task UpsertDocumentAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ParsedDocument>.Filter.Eq(item => item.Id, document.Id);
        return _documents.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task UpsertChunksAsync(IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            var filter = Builders<TextChunk>.Filter.Eq(item => item.Id, chunk.Id);
            await _chunks.ReplaceOneAsync(filter, chunk, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<TextChunk>> GetChunksAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TextChunk>.Filter.In(item => item.Id, chunkIds);
        var chunks = await _chunks.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
        var byId = chunks.ToDictionary(chunk => chunk.Id, StringComparer.Ordinal);
        return chunkIds.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
    }
}
