using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Pipelines;

public sealed class ChunkPreviewService(
    IDocumentParserResolver parserResolver,
    IChunkingStrategyFactory chunkingStrategyFactory,
    IOptions<ChunkingOptions> options) : IChunkPreviewService
{
    public async Task<IReadOnlyList<ChunkPreview>> PreviewAsync(string path, CancellationToken cancellationToken = default)
    {
        var parser = parserResolver.Resolve(path);
        var document = await parser.ParseAsync(path, cancellationToken).ConfigureAwait(false);
        var previews = new List<ChunkPreview>();
        foreach (var strategy in chunkingStrategyFactory.Strategies)
        {
            var chunks = await strategy.ChunkAsync(document, cancellationToken).ConfigureAwait(false);
            var sample = chunks.FirstOrDefault()?.Text ?? string.Empty;
            previews.Add(new ChunkPreview(
                strategy.Name,
                chunks.Count,
                chunks.Count == 0 ? 0 : chunks.Average(chunk => chunk.Text.Length),
                options.Value.Overlap,
                sample[..Math.Min(sample.Length, 160)]));
        }

        return previews;
    }
}
