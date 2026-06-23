using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IDocumentStore
{
    Task UpsertDocumentAsync(ParsedDocument document, CancellationToken cancellationToken = default);

    Task UpsertChunksAsync(IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TextChunk>> GetChunksAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default);
}
