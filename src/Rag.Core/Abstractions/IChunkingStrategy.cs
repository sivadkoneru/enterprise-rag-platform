using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IChunkingStrategy
{
    string Name { get; }

    Task<IReadOnlyList<TextChunk>> ChunkAsync(ParsedDocument document, CancellationToken cancellationToken = default);
}
