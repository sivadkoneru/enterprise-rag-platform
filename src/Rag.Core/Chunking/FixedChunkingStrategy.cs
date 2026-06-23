using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Chunking;

public sealed class FixedChunkingStrategy(IOptions<ChunkingOptions> options) : IChunkingStrategy
{
    public string Name => "fixed";

    public Task<IReadOnlyList<TextChunk>> ChunkAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var size = Math.Max(1, options.Value.Size);
        var overlap = Math.Clamp(options.Value.Overlap, 0, size - 1);
        var step = size - overlap;
        var chunks = new List<TextChunk>();
        for (var start = 0; start < document.Text.Length; start += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = Math.Min(document.Text.Length, start + size);
            chunks.Add(ChunkBuilder.Create(document, Name, chunks.Count, start, end));
            if (end == document.Text.Length)
            {
                break;
            }
        }

        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks.Where(chunk => chunk.Text.Length > 0).ToArray());
    }
}
