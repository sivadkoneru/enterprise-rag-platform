using System.Collections.Concurrent;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Stores;

public sealed class InMemoryDocumentStore : IDocumentStore, IChunkDeletionStore
{
    public Task DeleteChunksAsync(string documentId, IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in chunkIds) { cancellationToken.ThrowIfCancellationRequested(); if (_chunks.TryGetValue(id, out var chunk) && chunk.DocumentId == documentId) { _chunks.TryRemove(id, out _); } }
        return Task.CompletedTask;
    }

    private readonly ConcurrentDictionary<string, ParsedDocument> _documents = new();
    private readonly ConcurrentDictionary<string, TextChunk> _chunks = new();

    public Task UpsertDocumentAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task UpsertChunksAsync(IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _chunks[chunk.Id] = chunk;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TextChunk>> GetChunksAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        var chunks = chunkIds
            .Select(id =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _chunks.TryGetValue(id, out var chunk) ? chunk : null;
            })
            .Where(chunk => chunk is not null)
            .Cast<TextChunk>()
            .ToArray();
        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks);
    }
}
