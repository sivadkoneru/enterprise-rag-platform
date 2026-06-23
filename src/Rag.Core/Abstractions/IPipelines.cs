using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IIngestionPipeline
{
    Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default);

    Task<IngestionResult> IngestAsync(
        IngestionRequest request,
        IProgress<IngestionProgress>? progress,
        CancellationToken cancellationToken = default);
}

public interface IQueryPipeline
{
    Task<RagAnswer> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default);
}

public interface IChunkPreviewService
{
    Task<IReadOnlyList<ChunkPreview>> PreviewAsync(string path, CancellationToken cancellationToken = default);
}
