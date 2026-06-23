using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Chunking;

public sealed class SemanticChunkingStrategy(
    IEmbeddingClient embeddingClient,
    IChatClient chatClient,
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
            if (previous is not null && CosineDistance(previous, current) >= threshold)
            {
                chunks.Add(ChunkBuilder.Create(document, Name, chunks.Count, chunkStart, start));
                chunkStart = start;
            }

            previous = current;
            cursor = start + paragraph.Length;
        }

        chunks.Add(ChunkBuilder.Create(document, Name, chunks.Count, chunkStart, document.Text.Length));
        if (options.Value.SemanticRefineWithChat)
        {
            await chatClient.CompleteAsync([new ChatMessage("system", "Refine chunk boundaries without changing text."), new ChatMessage("user", document.Id)], cancellationToken).ConfigureAwait(false);
        }

        return chunks.Where(chunk => chunk.Text.Length > 0).ToArray();
    }

    private static double CosineDistance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var length = Math.Min(left.Count, right.Count);
        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 1;
        }

        return 1 - dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
