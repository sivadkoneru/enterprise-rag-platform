using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IIngestionJobStore
{
    Task<IngestionJob> CreateAsync(IngestionRequest request, CancellationToken cancellationToken = default);

    Task<IngestionJob?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default);

    Task UpdateProgressAsync(string jobId, IngestionProgress progress, CancellationToken cancellationToken = default);

    Task MarkRunningAsync(string jobId, CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(string jobId, IngestionResult result, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(string jobId, string error, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngestionJob>> GetRestartableJobsAsync(CancellationToken cancellationToken = default);

    Task<IngestionJob?> TryAcquireAsync(string jobId, string workerId, CancellationToken cancellationToken = default);
}

public interface IIngestionJobQueue
{
    Task<IngestionJob> EnqueueAsync(IngestionRequest request, CancellationToken cancellationToken = default);

    Task EnqueueExistingAsync(IngestionJob job, CancellationToken cancellationToken = default);

    IAsyncEnumerable<IngestionJob> DequeueAllAsync(CancellationToken cancellationToken = default);
}
