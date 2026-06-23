using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Microsoft.Extensions.Options;

namespace Rag.Core.Pipelines;

public sealed class IngestionPipeline(
    IDocumentSourceResolver sourceResolver,
    IDocumentParserResolver parserResolver,
    IChunkingStrategyFactory chunkingStrategyFactory,
    IEmbeddingClient embeddingClient,
    IDocumentStore documentStore,
    IVectorStore vectorStore,
    IOptions<IngestionOptions> ingestionOptions) : IIngestionPipeline
{
    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        var allChunkIds = new List<string>();
        var allDocumentIds = new List<string>();
        var strategy = chunkingStrategyFactory.Resolve(request.Strategy);
        var sourceUris = request.SourceUris;

        if (sourceUris.Count == 0)
        {
            throw new ArgumentException("At least one ingestion source is required.", nameof(request));
        }

        await vectorStore.EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        var sourceItems = new List<SourceItem>();
        try
        {
            foreach (var sourceUri in sourceUris)
            {
                var source = sourceResolver.Resolve(sourceUri);
                await foreach (var item in source.EnumerateAsync(sourceUri, cancellationToken).ConfigureAwait(false))
                {
                    sourceItems.Add(item);
                }
            }

            await Parallel.ForEachAsync(
                sourceItems,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Max(1, ingestionOptions.Value.MaxDegreeOfParallelism)
                },
                async (item, token) =>
                {
                    var result = await IngestItemAsync(item, strategy, token).ConfigureAwait(false);
                    lock (allChunkIds)
                    {
                        allChunkIds.AddRange(result.ChunkIds);
                        allDocumentIds.Add(result.DocumentId);
                    }
                }).ConfigureAwait(false);
        }
        finally
        {
            foreach (var item in sourceItems)
            {
                await item.DisposeAsync().ConfigureAwait(false);
            }
        }

        return new IngestionResult(allDocumentIds.LastOrDefault() ?? string.Empty, allChunkIds.Count, strategy.Name, allChunkIds, allDocumentIds);
    }

    private async Task<IngestionResult> IngestItemAsync(SourceItem item, IChunkingStrategy strategy, CancellationToken cancellationToken)
    {
        var parser = parserResolver.Resolve(item.LocalPath);
        var parsedDocument = await parser.ParseAsync(item.LocalPath, cancellationToken).ConfigureAwait(false);
        var document = EnrichDocument(parsedDocument, item);
        var chunks = await strategy.ChunkAsync(document, cancellationToken).ConfigureAwait(false);
        var vectors = new List<VectorRecord>();

        foreach (var chunk in chunks)
        {
            var vector = await embeddingClient.EmbedAsync(chunk.Text, cancellationToken).ConfigureAwait(false);
            vectors.Add(new VectorRecord(
                chunk.Id,
                chunk.DocumentId,
                vector,
                new Dictionary<string, string>
                {
                    ["source"] = chunk.Metadata.Source,
                    ["origin"] = chunk.Metadata.Origin,
                    ["fileName"] = chunk.Metadata.FileName,
                    ["fileType"] = FileType(chunk.Metadata.Extension),
                    ["extension"] = chunk.Metadata.Extension,
                    ["chunkIndex"] = chunk.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        await documentStore.UpsertDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        await documentStore.UpsertChunksAsync(chunks, cancellationToken).ConfigureAwait(false);
        await vectorStore.UpsertAsync(vectors, cancellationToken).ConfigureAwait(false);

        return new IngestionResult(document.Id, chunks.Count, strategy.Name, chunks.Select(chunk => chunk.Id).ToArray(), [document.Id]);
    }

    private static ParsedDocument EnrichDocument(ParsedDocument document, SourceItem item)
    {
        var documentId = StableId(item.Origin, item.Source);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (document.Metadata.Attributes is not null)
        {
            foreach (var attribute in document.Metadata.Attributes)
            {
                attributes[attribute.Key] = attribute.Value;
            }
        }

        if (item.Attributes is not null)
        {
            foreach (var attribute in item.Attributes)
            {
                attributes[attribute.Key] = attribute.Value;
            }
        }

        var metadata = document.Metadata with
        {
            DocumentId = documentId,
            Source = item.Source,
            FileName = item.FileName,
            Extension = item.Extension,
            Origin = item.Origin,
            Attributes = attributes
        };

        return new ParsedDocument(documentId, document.Text, metadata);
    }

    private static string StableId(string origin, string source)
    {
        var input = $"{origin}:{source}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)))[..24].ToLowerInvariant();
    }

    private static string FileType(string extension)
    {
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
    }
}
