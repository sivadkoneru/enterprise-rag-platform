using System.Collections.Concurrent;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Jobs;

public sealed class InMemoryIngestionJobStore : IIngestionJobStore
{
    private readonly ConcurrentDictionary<string, IngestionJob> _jobs = new();

    public Task<IngestionJob> CreateAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var job = new IngestionJob(
            Guid.NewGuid().ToString("N"),
            request,
            IngestionJobStatus.Queued,
            CreatedAt: DateTimeOffset.UtcNow);
        _jobs[job.Id] = job;
        return Task.FromResult(job);
    }

    public Task<IngestionJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task MarkRunningAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(jobId, job => job with
        {
            Status = IngestionJobStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task MarkSucceededAsync(string jobId, IngestionResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(jobId, job => job with
        {
            Status = IngestionJobStatus.Succeeded,
            DocumentCount = result.DocumentIds?.Count ?? (string.IsNullOrWhiteSpace(result.DocumentId) ? 0 : 1),
            ChunkCount = result.ChunkCount,
            DocumentIds = result.DocumentIds ?? (string.IsNullOrWhiteSpace(result.DocumentId) ? [] : [result.DocumentId]),
            ChunkIds = result.ChunkIds,
            CompletedAt = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(string jobId, string error, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(jobId, job => job with
        {
            Status = IngestionJobStatus.Failed,
            Error = error,
            CompletedAt = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    private void Update(string jobId, Func<IngestionJob, IngestionJob> update)
    {
        _jobs.AddOrUpdate(
            jobId,
            id => throw new KeyNotFoundException($"Ingestion job '{id}' was not found."),
            (_, existing) => update(existing));
    }
}
