using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Microsoft.Extensions.Options;

namespace Rag.Core.Pipelines;

public sealed class IngestionPipeline(
    IDocumentSourceResolver sourceResolver,
    IDocumentParserResolver parserResolver,
    IEnumerable<IMultiDocumentParser> multiDocumentParsers,
    IChunkingStrategyFactory chunkingStrategyFactory,
    IEmbeddingClient embeddingClient,
    IDocumentStore documentStore,
    IVectorStore vectorStore,
    IOptions<IngestionOptions> ingestionOptions) : IIngestionPipeline
{
    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        return await IngestAsync(request, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IngestionResult> IngestAsync(
        IngestionRequest request,
        IProgress<IngestionProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var allChunkIds = new List<string>();
        var allDocumentIds = new List<string>();
        var processedSourceCount = 0;
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

            ReportProgress(progress, sourceItems.Count, processedSourceCount, allDocumentIds, allChunkIds, null);

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
                        if (result.DocumentIds is { Count: > 0 })
                        {
                            allDocumentIds.AddRange(result.DocumentIds);
                        }
                        else if (!string.IsNullOrWhiteSpace(result.DocumentId))
                        {
                            allDocumentIds.Add(result.DocumentId);
                        }

                        processedSourceCount++;
                        ReportProgress(progress, sourceItems.Count, processedSourceCount, allDocumentIds, allChunkIds, item.Source);
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

        ReportProgress(progress, sourceItems.Count, processedSourceCount, allDocumentIds, allChunkIds, null);
        return new IngestionResult(allDocumentIds.LastOrDefault() ?? string.Empty, allChunkIds.Count, strategy.Name, allChunkIds, allDocumentIds);
    }

    private async Task<IngestionResult> IngestItemAsync(SourceItem item, IChunkingStrategy strategy, CancellationToken cancellationToken)
    {
        var documents = await ParseDocumentsAsync(item, cancellationToken).ConfigureAwait(false);
        var documentIds = new List<string>();
        var chunkIds = new List<string>();

        foreach (var parsedDocument in documents)
        {
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
                    VectorMetadata(chunk)));
            }

            await documentStore.UpsertDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            await documentStore.UpsertChunksAsync(chunks, cancellationToken).ConfigureAwait(false);
            await vectorStore.UpsertAsync(vectors, cancellationToken).ConfigureAwait(false);

            documentIds.Add(document.Id);
            chunkIds.AddRange(chunks.Select(chunk => chunk.Id));
        }

        return new IngestionResult(documentIds.LastOrDefault() ?? string.Empty, chunkIds.Count, strategy.Name, chunkIds, documentIds);
    }

    private async Task<IReadOnlyList<ParsedDocument>> ParseDocumentsAsync(SourceItem item, CancellationToken cancellationToken)
    {
        var multiParser = multiDocumentParsers.FirstOrDefault(parser => parser.CanParse(item.LocalPath));
        if (multiParser is not null)
        {
            var documents = new List<ParsedDocument>();
            await foreach (var document in multiParser.ParseManyAsync(item.LocalPath, item.Attributes, cancellationToken).ConfigureAwait(false))
            {
                documents.Add(document);
            }

            return documents;
        }

        var parser = parserResolver.Resolve(item.LocalPath);
        return [await parser.ParseAsync(item.LocalPath, cancellationToken).ConfigureAwait(false)];
    }

    private static ParsedDocument EnrichDocument(ParsedDocument document, SourceItem item)
    {
        var documentId = StableId(item.Origin, item.Source, RecordKey(document));
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

    private static Dictionary<string, string> VectorMetadata(TextChunk chunk)
    {
        var metadata = new Dictionary<string, string>
        {
            ["source"] = chunk.Metadata.Source,
            ["origin"] = chunk.Metadata.Origin,
            ["fileName"] = chunk.Metadata.FileName,
            ["fileType"] = FileType(chunk.Metadata.Extension),
            ["extension"] = chunk.Metadata.Extension,
            ["chunkIndex"] = chunk.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (chunk.Metadata.Attributes is not null &&
            chunk.Metadata.Attributes.TryGetValue("recordKey", out var recordKey) &&
            !string.IsNullOrWhiteSpace(recordKey))
        {
            metadata["recordKey"] = recordKey;
        }

        return metadata;
    }

    private static string? RecordKey(ParsedDocument document)
    {
        return document.Metadata.Attributes is not null &&
            document.Metadata.Attributes.TryGetValue("recordKey", out var recordKey) &&
            !string.IsNullOrWhiteSpace(recordKey)
                ? recordKey
                : null;
    }

    private static string StableId(string origin, string source, string? recordKey)
    {
        var input = string.IsNullOrWhiteSpace(recordKey) ? $"{origin}:{source}" : $"{origin}:{source}:{recordKey}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)))[..24].ToLowerInvariant();
    }

    private static string FileType(string extension)
    {
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
    }

    private static void ReportProgress(
        IProgress<IngestionProgress>? progress,
        int totalSourceCount,
        int processedSourceCount,
        IReadOnlyList<string> documentIds,
        IReadOnlyList<string> chunkIds,
        string? currentSource)
    {
        progress?.Report(new IngestionProgress(
            totalSourceCount,
            processedSourceCount,
            documentIds.Count,
            chunkIds.Count,
            documentIds.ToArray(),
            chunkIds.ToArray(),
            currentSource,
            DateTimeOffset.UtcNow));
    }
}
