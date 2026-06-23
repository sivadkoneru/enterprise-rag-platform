using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Pipelines;

public sealed class ChunkPreviewService(
    IDocumentSourceResolver sourceResolver,
    IDocumentParserResolver parserResolver,
    IChunkingStrategyFactory chunkingStrategyFactory,
    IOptions<ChunkingOptions> options,
    ILogger<ChunkPreviewService> logger) : IChunkPreviewService
{
    private const int SampleLength = 160;

    public async Task<IReadOnlyList<ChunkPreview>> PreviewAsync(string path, CancellationToken cancellationToken = default)
    {
        // Preview resolves through the same source adapters as ingestion so that path rules,
        // schema sidecar discovery, and cloud materialization behave identically.
        var source = sourceResolver.Resolve(path);
        await using var items = source.EnumerateAsync(path, cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (!await items.MoveNextAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException($"No previewable document was found for '{path}'.");
        }

        var item = items.Current;
        try
        {
            var document = await ParseFirstDocumentAsync(item, cancellationToken).ConfigureAwait(false);
            var previews = new List<ChunkPreview>();
            foreach (var strategy in chunkingStrategyFactory.Strategies)
            {
                var chunks = await strategy.ChunkAsync(document, cancellationToken).ConfigureAwait(false);
                var sample = chunks.Count == 0 ? string.Empty : chunks[0].Text;
                previews.Add(new ChunkPreview(
                    strategy.Name,
                    chunks.Count,
                    chunks.Count == 0 ? 0 : chunks.Average(chunk => chunk.Text.Length),
                    options.Value.Overlap,
                    sample[..Math.Min(sample.Length, SampleLength)]));
            }

            logger.LogDebug(
                "Previewed {Path} with {StrategyCount} chunking strategy/strategies.",
                path,
                previews.Count);
            return previews;
        }
        finally
        {
            await item.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<ParsedDocument> ParseFirstDocumentAsync(SourceItem item, CancellationToken cancellationToken)
    {
        await foreach (var document in parserResolver
            .ParseAsync(item.LocalPath, item.Attributes, cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            return document;
        }

        throw new InvalidOperationException($"Structured document '{item.Source}' did not produce any documents.");
    }
}
