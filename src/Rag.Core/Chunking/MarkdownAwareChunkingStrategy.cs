using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Chunking;

public sealed class MarkdownAwareChunkingStrategy(IOptions<ChunkingOptions> options) : IChunkingStrategy
{
    public string Name => "markdown";

    public Task<IReadOnlyList<TextChunk>> ChunkAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var size = Math.Max(1, options.Value.Size);
        var sections = document.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunks = new List<TextChunk>();
        var cursor = 0;
        var currentStart = 0;
        var currentLength = 0;

        foreach (var section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var position = document.Text.IndexOf(section, cursor, StringComparison.Ordinal);
            if (position < 0)
            {
                position = cursor;
            }

            if (currentLength == 0)
            {
                currentStart = position;
            }

            if (currentLength > 0 && currentLength + section.Length > size)
            {
                chunks.Add(ChunkBuilder.Create(document, Name, chunks.Count, currentStart, cursor));
                currentStart = position;
                currentLength = 0;
            }

            currentLength += section.Length + 2;
            cursor = position + section.Length;
        }

        if (currentLength > 0)
        {
            chunks.Add(ChunkBuilder.Create(document, Name, chunks.Count, currentStart, document.Text.Length));
        }

        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks.Where(chunk => chunk.Text.Length > 0).ToArray());
    }
}
