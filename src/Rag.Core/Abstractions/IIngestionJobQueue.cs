using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IIngestionJobQueue
{
    Task<IngestionJob> EnqueueAsync(IngestionRequest request, CancellationToken cancellationToken = default);

    Task EnqueueExistingAsync(IngestionJob job, CancellationToken cancellationToken = default);

    IAsyncEnumerable<IngestionJob> DequeueAllAsync(CancellationToken cancellationToken = default);
}
