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
        await RecoverRestartableJobsAsync(stoppingToken).ConfigureAwait(false);

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

                var progress = new JobStoreProgress(job.Id, jobStore);
                var result = await pipeline.IngestAsync(acquired.Request, progress, stoppingToken).ConfigureAwait(false);
                await jobStore.MarkSucceededAsync(job.Id, result, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ingestion job {JobId} failed.", job.Id);
                await jobStore.MarkFailedAsync(job.Id, exception.Message, CancellationToken.None).ConfigureAwait(false);
            }
        }
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

    private sealed class JobStoreProgress(string jobId, IIngestionJobStore jobStore) : IProgress<IngestionProgress>
    {
        public void Report(IngestionProgress value)
        {
            jobStore.UpdateProgressAsync(jobId, value, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
