using Rag.Core.Abstractions;
using Rag.Core.Common;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Rag.Core.Pipelines;

public sealed class IngestionPipeline(
    IDocumentSourceResolver sourceResolver,
    IDocumentParserResolver parserResolver,
    IChunkingStrategyFactory chunkingStrategyFactory,
    IEmbeddingClient embeddingClient,
    IDocumentStore documentStore,
    IVectorStore vectorStore,
    IOptions<IngestionOptions> ingestionOptions,
    ILogger<IngestionPipeline> logger) : IIngestionPipeline
{
    /// <summary>Smallest gap between intermediate progress snapshots during a run.</summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);

    public async Task<IngestionResult> IngestAsync(
        IngestionRequest request,
        IProgress<IngestionProgress>? progress,
        Func<CancellationToken, Task<IngestionJobStatus>>? statusProvider,
        CancellationToken cancellationToken = default)
    {
        var allChunkIds = new List<string>();
        var allDocumentIds = new List<string>();
        var progressLock = new Lock();
        var processedSourceCount = 0;
        var discoveredSourceCount = 0;
        var strategy = chunkingStrategyFactory.Resolve(request.Strategy);
        var sourceUris = request.SourceUris;

        if (sourceUris.Count == 0)
        {
            throw new ArgumentException("At least one ingestion source is required.", nameof(request));
        }

        logger.LogInformation("Starting ingestion run for {SourceCount} source(s).", sourceUris.Count);

        await vectorStore.EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        await ThrowIfJobStoppedAsync(statusProvider, cancellationToken).ConfigureAwait(false);

        var progressClock = System.Diagnostics.Stopwatch.StartNew();
        var lastReportedAt = TimeSpan.Zero;
        progress?.Report(BuildProgress(0, 0, allDocumentIds, allChunkIds, null));

        // Source items are streamed rather than collected up front: cloud adapters materialize each
        // object to a temporary file as it is yielded, so buffering the whole prefix would hold the
        // entire corpus on local disk before any of it is processed.
        var items = EnumerateSourceItemsAsync(
            sourceUris,
            () => Interlocked.Increment(ref discoveredSourceCount),
            cancellationToken);

        await Parallel.ForEachAsync(
            items,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, ingestionOptions.Value.MaxDegreeOfParallelism)
            },
            async (item, token) =>
            {
                try
                {
                    await ThrowIfJobStoppedAsync(statusProvider, token).ConfigureAwait(false);
                    var result = await IngestItemAsync(item, strategy, token).ConfigureAwait(false);
                    logger.LogDebug(
                        "Ingested source item {Source}: {DocumentCount} document(s), {ChunkCount} chunk(s).",
                        item.Source,
                        result.DocumentIds.Count,
                        result.ChunkIds.Count);
                    IngestionProgress? snapshot = null;
                    lock (progressLock)
                    {
                        allChunkIds.AddRange(result.ChunkIds);
                        allDocumentIds.AddRange(result.DocumentIds);
                        processedSourceCount++;

                        // Each snapshot copies the accumulated id lists, so rate-limit them instead
                        // of paying that cost (and a job-store write) once per source item. The
                        // exact final state is always reported after the loop.
                        var elapsed = progressClock.Elapsed;
                        if (progress is not null && elapsed - lastReportedAt >= ProgressInterval)
                        {
                            lastReportedAt = elapsed;
                            snapshot = BuildProgress(
                                Volatile.Read(ref discoveredSourceCount),
                                processedSourceCount,
                                allDocumentIds,
                                allChunkIds,
                                item.Source);
                        }
                    }

                    // Progress sinks can persist to a remote job store, so report outside the lock.
                    if (snapshot is not null)
                    {
                        progress?.Report(snapshot);
                    }

                    await ThrowIfJobStoppedAsync(statusProvider, token).ConfigureAwait(false);
                }
                finally
                {
                    await item.DisposeAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

        await ThrowIfJobStoppedAsync(statusProvider, cancellationToken).ConfigureAwait(false);
        progress?.Report(BuildProgress(discoveredSourceCount, processedSourceCount, allDocumentIds, allChunkIds, null));
        logger.LogInformation(
            "Ingestion run completed: {DocumentCount} document(s), {ChunkCount} chunk(s) in {ElapsedMilliseconds} ms.",
            allDocumentIds.Count,
            allChunkIds.Count,
            progressClock.ElapsedMilliseconds);
        return new IngestionResult(allChunkIds.Count, strategy.Name, allChunkIds, allDocumentIds);
    }

    private async IAsyncEnumerable<SourceItem> EnumerateSourceItemsAsync(
        IReadOnlyList<string> sourceUris,
        Action onDiscovered,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var sourceUri in sourceUris)
        {
            var source = sourceResolver.Resolve(sourceUri);
            await foreach (var item in source.EnumerateAsync(sourceUri, cancellationToken).ConfigureAwait(false))
            {
                onDiscovered();
                yield return item;
            }
        }
    }

    private async Task<IngestionResult> IngestItemAsync(SourceItem item, IChunkingStrategy strategy, CancellationToken cancellationToken)
    {
        var documents = await ParseDocumentsAsync(item, cancellationToken).ConfigureAwait(false);
        if (documents.Count == 0)
        {
            logger.LogWarning("Source item {Source} yielded zero documents.", item.Source);
        }

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

        return new IngestionResult(chunkIds.Count, strategy.Name, chunkIds, documentIds);
    }

    private async Task<IReadOnlyList<ParsedDocument>> ParseDocumentsAsync(SourceItem item, CancellationToken cancellationToken)
    {
        var documents = new List<ParsedDocument>();
        await foreach (var document in parserResolver
            .ParseAsync(item.LocalPath, item.Attributes, cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            documents.Add(document);
        }

        return documents;
    }

    private static ParsedDocument EnrichDocument(ParsedDocument document, SourceItem item)
    {
        var recordKey = RecordKey(document);
        var documentId = StableId.Compute(string.IsNullOrWhiteSpace(recordKey)
            ? $"{item.Origin}:{item.Source}"
            : $"{item.Origin}:{item.Source}:{recordKey}");
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
            ["fileType"] = FileTypes.Normalize(chunk.Metadata.Extension),
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

    private static async Task ThrowIfJobStoppedAsync(
        Func<CancellationToken, Task<IngestionJobStatus>>? statusProvider,
        CancellationToken cancellationToken)
    {
        if (statusProvider is null)
        {
            return;
        }

        var status = await statusProvider(cancellationToken).ConfigureAwait(false);
        if (status == IngestionJobStatus.Paused)
        {
            throw new IngestionJobPausedException();
        }

        if (status == IngestionJobStatus.Canceled)
        {
            throw new IngestionJobCanceledException();
        }
    }

    private static IngestionProgress BuildProgress(
        int totalSourceCount,
        int processedSourceCount,
        IReadOnlyList<string> documentIds,
        IReadOnlyList<string> chunkIds,
        string? currentSource)
    {
        return new IngestionProgress(
            totalSourceCount,
            processedSourceCount,
            documentIds.Count,
            chunkIds.Count,
            documentIds.ToArray(),
            chunkIds.ToArray(),
            currentSource,
            DateTimeOffset.UtcNow);
    }
}
