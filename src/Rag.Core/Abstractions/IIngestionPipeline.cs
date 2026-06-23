using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IIngestionPipeline
{
    Task<IngestionResult> IngestAsync(
        IngestionRequest request,
        IProgress<IngestionProgress>? progress,
        Func<CancellationToken, Task<IngestionJobStatus>>? statusProvider,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Convenience overloads for the common ingestion calls. These are extension methods rather than
/// interface members so that every implementation and test double only has to satisfy the single
/// full-signature method above.
/// </summary>
public static class IngestionPipelineExtensions
{
    public static Task<IngestionResult> IngestAsync(
        this IIngestionPipeline pipeline,
        IngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.IngestAsync(request, null, null, cancellationToken);
    }

    public static Task<IngestionResult> IngestAsync(
        this IIngestionPipeline pipeline,
        IngestionRequest request,
        IProgress<IngestionProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.IngestAsync(request, progress, null, cancellationToken);
    }
}
