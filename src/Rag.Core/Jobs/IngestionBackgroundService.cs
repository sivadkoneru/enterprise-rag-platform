using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;

namespace Rag.Core.Jobs;

public sealed class IngestionBackgroundService(
    IIngestionJobQueue queue,
    IIngestionJobStore jobStore,
    IIngestionPipeline pipeline,
    ILogger<IngestionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.DequeueAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await jobStore.MarkRunningAsync(job.Id, stoppingToken).ConfigureAwait(false);
                var result = await pipeline.IngestAsync(job.Request, stoppingToken).ConfigureAwait(false);
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
}
