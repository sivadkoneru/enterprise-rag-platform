using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Pipelines;

public sealed class IngestionPipeline(
    IDocumentParserResolver parserResolver,
    IChunkingStrategyFactory chunkingStrategyFactory,
    IEmbeddingClient embeddingClient,
    IDocumentStore documentStore,
    IVectorStore vectorStore) : IIngestionPipeline
{
    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        var files = EnumerateFiles(request.Path);
        var allChunkIds = new List<string>();
        string? lastDocumentId = null;
        var strategy = chunkingStrategyFactory.Resolve(request.Strategy);

        await vectorStore.EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        foreach (var file in files)
        {
            var parser = parserResolver.Resolve(file);
            var document = await parser.ParseAsync(file, cancellationToken).ConfigureAwait(false);
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
                        ["fileName"] = chunk.Metadata.FileName,
                        ["chunkIndex"] = chunk.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }));
            }

            await documentStore.UpsertDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            await documentStore.UpsertChunksAsync(chunks, cancellationToken).ConfigureAwait(false);
            await vectorStore.UpsertAsync(vectors, cancellationToken).ConfigureAwait(false);

            allChunkIds.AddRange(chunks.Select(chunk => chunk.Id));
            lastDocumentId = document.Id;
        }

        return new IngestionResult(lastDocumentId ?? string.Empty, allChunkIds.Count, strategy.Name, allChunkIds);
    }

    private static IReadOnlyList<string> EnumerateFiles(string path)
    {
        if (File.Exists(path))
        {
            return [path];
        }

        if (!Directory.Exists(path))
        {
            throw new FileNotFoundException($"Path '{path}' does not exist.");
        }

        return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => IsSupported(file))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsSupported(string file)
    {
        var extension = Path.GetExtension(file);
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
