using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IIngestionJobStore
{
    Task<IngestionJob> CreateAsync(IngestionRequest request, CancellationToken cancellationToken = default);

    Task<IngestionJob?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default);

    Task MarkRunningAsync(string jobId, CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(string jobId, IngestionResult result, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(string jobId, string error, CancellationToken cancellationToken = default);
}

public interface IIngestionJobQueue
{
    Task<IngestionJob> EnqueueAsync(IngestionRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<IngestionJob> DequeueAllAsync(CancellationToken cancellationToken = default);
}
