using System.Threading.Channels;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Jobs;

public sealed class IngestionJobQueue(IIngestionJobStore jobStore) : IIngestionJobQueue
{
    private readonly Channel<IngestionJob> _queue = Channel.CreateUnbounded<IngestionJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public async Task<IngestionJob> EnqueueAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        var job = await jobStore.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        await EnqueueExistingAsync(job, cancellationToken).ConfigureAwait(false);
        return job;
    }

    public async Task EnqueueExistingAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(job, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<IngestionJob> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}
