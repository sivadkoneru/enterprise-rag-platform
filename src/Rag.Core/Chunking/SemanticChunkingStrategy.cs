using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Common;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Chunking;

public sealed class SemanticChunkingStrategy(
    IEmbeddingClient embeddingClient,
    IOptions<ChunkingOptions> options) : IChunkingStrategy
{
    public string Name => "semantic";

    public async Task<IReadOnlyList<TextChunk>> ChunkAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var paragraphs = document.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paragraphs.Length <= 1)
        {
            return [ChunkBuilder.Create(document, Name, 0, 0, document.Text.Length)];
        }

        var threshold = options.Value.SemanticDistanceThreshold;
        var chunks = new List<TextChunk>();
        var chunkStart = 0;
        var cursor = 0;
        IReadOnlyList<float>? previous = null;

        foreach (var paragraph in paragraphs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = document.Text.IndexOf(paragraph, cursor, StringComparison.Ordinal);
            if (start < 0)
            {
                start = cursor;
            }

            var current = await embeddingClient.EmbedAsync(paragraph, cancellationToken).ConfigureAwait(false);

            // Distance is expressed as 1 - similarity. When either vector has zero magnitude,
            // VectorMath.CosineSimilarity returns 0, so distance is 1 - 0 == 1 (maximally
            // distant), matching this method's previous standalone zero-magnitude behavior.
            if (previous is not null && 1 - VectorMath.CosineSimilarity(previous, current) >= threshold)
            {
                chunks.Add(ChunkBuilder.Create(document, Name, chunks.Count, chunkStart, start));
                chunkStart = start;
            }

            previous = current;
            cursor = start + paragraph.Length;
        }

        chunks.Add(ChunkBuilder.Create(document, Name, chunks.Count, chunkStart, document.Text.Length));
        return chunks.Where(chunk => chunk.Text.Length > 0).ToArray();
    }
}
