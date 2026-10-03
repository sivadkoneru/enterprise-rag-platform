using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Jobs;

public sealed class IngestionBackgroundService(
    IIngestionJobQueue queue,
    IIngestionJobStore jobStore,
    IIngestionPipeline pipeline,
    ILogger<IngestionBackgroundService> logger) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var recoveryRetry = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverRestartableJobsAsync(stoppingToken).ConfigureAwait(false);
                break;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Ingestion recovery could not contact the configured job store ({ErrorType}); retrying.", exception.GetType().Name);
                await recoveryRetry.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
        }

        await foreach (var job in queue.DequeueAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var acquired = await jobStore.TryAcquireAsync(job.Id, _workerId, stoppingToken).ConfigureAwait(false);
                if (acquired is null)
                {
                    logger.LogDebug("Skipping ingestion job {JobId}; it is not queued or was acquired by another worker.", job.Id);
                    continue;
                }

                var progress = new JobStoreProgress(job.Id, jobStore, logger);
                try
                {
                    var result = await pipeline.IngestAsync(
                        acquired.Request,
                        progress,
                        token => CurrentStatusAsync(job.Id, token),
                        stoppingToken).ConfigureAwait(false);

                    // Flush the final progress snapshot before the job is marked succeeded.
                    await progress.CompleteAsync().ConfigureAwait(false);
                    await jobStore.MarkSucceededAsync(job.Id, result, stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    await progress.CompleteAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (IngestionJobPausedException)
            {
                logger.LogInformation("Ingestion job {JobId} paused.", job.Id);
                await jobStore.MarkPausedAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
            }
            catch (IngestionJobCanceledException)
            {
                logger.LogInformation("Ingestion job {JobId} canceled.", job.Id);
                await jobStore.MarkCanceledAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                logger.LogError( "Ingestion job {JobId} failed.", job.Id);
                await jobStore.MarkFailedAsync(job.Id, "Ingestion failed. Check server diagnostics.", CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task<IngestionJobStatus> CurrentStatusAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = await jobStore.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        return job?.Status ?? IngestionJobStatus.Canceled;
    }

    private async Task RecoverRestartableJobsAsync(CancellationToken cancellationToken)
    {
        var restartableJobs = await jobStore.GetRestartableJobsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var job in restartableJobs)
        {
            var requeued = job with
            {
                Status = IngestionJobStatus.Queued,
                WorkerId = null,
                Error = null,
                UpdatedAt = DateTimeOffset.UtcNow,
                StartedAt = job.Status == IngestionJobStatus.Running ? null : job.StartedAt,
                CompletedAt = null
            };
            await jobStore.UpdateAsync(requeued, cancellationToken).ConfigureAwait(false);
            await queue.EnqueueExistingAsync(requeued, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Recovered ingestion job {JobId} for processing.", requeued.Id);
        }
    }

    /// <summary>
    /// Bridges the synchronous <see cref="IProgress{T}"/> callback used by <see cref="IIngestionPipeline"/>
    /// (invoked from inside <c>Parallel.ForEachAsync</c> worker callbacks) to the async job store without
    /// blocking a thread-pool thread on a database round trip. Reported snapshots are handed to a
    /// bounded, latest-value-wins channel and persisted by a single background drain loop.
    /// </summary>
    private sealed class JobStoreProgress : IProgress<IngestionProgress>
    {
        private readonly Channel<IngestionProgress> _channel = Channel.CreateBounded<IngestionProgress>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        private readonly string _jobId;
        private readonly IIngestionJobStore _jobStore;
        private readonly ILogger _logger;
        private readonly Task _drainTask;

        public JobStoreProgress(string jobId, IIngestionJobStore jobStore, ILogger logger)
        {
            _jobId = jobId;
            _jobStore = jobStore;
            _logger = logger;

            // Started last, so the drain loop never observes a partially initialized instance.
            _drainTask = DrainAsync();
        }

        public void Report(IngestionProgress value)
        {
            _channel.Writer.TryWrite(value);
        }

        public async Task CompleteAsync()
        {
            _channel.Writer.TryComplete();
            await _drainTask.ConfigureAwait(false);
        }

        private async Task DrainAsync()
        {
            await foreach (var snapshot in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    await _jobStore.UpdateProgressAsync(_jobId, snapshot, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // A progress write is best-effort: losing one snapshot must not fail the job.
                    _logger.LogWarning("Failed to persist ingestion progress for job {JobId}.", _jobId);
                }
            }
        }
    }
}
