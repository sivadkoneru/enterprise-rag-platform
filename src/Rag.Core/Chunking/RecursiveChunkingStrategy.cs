using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Chunking;

public sealed class RecursiveChunkingStrategy(IOptions<ChunkingOptions> options) : IChunkingStrategy
{
    private static readonly string[] Separators = ["\n\n", "\n", ". ", " "];

    public string Name => "recursive";

    public Task<IReadOnlyList<TextChunk>> ChunkAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var size = Math.Max(1, options.Value.Size);
        var ranges = Split(document.Text, 0, size, 0, cancellationToken).ToArray();
        var chunks = ranges.Select((range, index) => ChunkBuilder.Create(document, Name, index, range.Start, range.End))
            .Where(chunk => chunk.Text.Length > 0)
            .ToArray();
        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks);
    }

    private static IEnumerable<(int Start, int End)> Split(string text, int offset, int size, int separatorIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text.Length <= size)
        {
            yield return (offset, offset + text.Length);
            yield break;
        }

        if (separatorIndex >= Separators.Length)
        {
            for (var start = 0; start < text.Length; start += size)
            {
                yield return (offset + start, offset + Math.Min(text.Length, start + size));
            }

            yield break;
        }

        var separator = Separators[separatorIndex];
        var cursor = 0;
        while (cursor < text.Length)
        {
            var end = Math.Min(text.Length, cursor + size);
            var splitAt = text.LastIndexOf(separator, end - 1, end - cursor, StringComparison.Ordinal);
            if (splitAt <= cursor)
            {
                foreach (var child in Split(text[cursor..end], offset + cursor, size, separatorIndex + 1, cancellationToken))
                {
                    yield return child;
                }

                cursor = end;
            }
            else
            {
                yield return (offset + cursor, offset + splitAt + separator.Length);
                cursor = splitAt + separator.Length;
            }
        }
    }
}
