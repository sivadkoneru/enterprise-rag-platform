using System.Threading.Channels;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Jobs;

public sealed class IngestionJobQueue(IIngestionJobStore jobStore) : IIngestionJobQueue, IDisposable
{
    // The store owns pending work; the bounded channel only wakes the single reader.
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private readonly SemaphoreSlim _admission = new(1, 1);

    public async Task<IngestionJob> EnqueueAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        await _admission.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if ((await jobStore.GetRestartableJobsAsync(cancellationToken).ConfigureAwait(false)).Count >= 32)
            { throw new IngestionCapacityException(); }
            var job = await jobStore.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            await EnqueueExistingAsync(job, cancellationToken).ConfigureAwait(false);
            return job;
        }
        finally { _admission.Release(); }
    }

    public Task EnqueueExistingAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _wake.Writer.TryWrite(true);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<IngestionJob> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pending = (await jobStore.GetRestartableJobsAsync(cancellationToken).ConfigureAwait(false)).Where(job => job.Status == IngestionJobStatus.Queued).OrderBy(job => job.CreatedAt).ToArray();
            if (pending.Length == 0) { await _wake.Reader.ReadAsync(cancellationToken).ConfigureAwait(false); }
            foreach (var job in pending) { yield return job; }
        }
    }
    public void Dispose() => _admission.Dispose();
}

public sealed class IngestionCapacityException : Exception;
