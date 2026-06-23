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
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
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
        _jobs[job.Id] = job with { UpdatedAt = Now(job.UpdatedAt) };
        return Task.CompletedTask;
    }

    public Task UpdateProgressAsync(string jobId, IngestionProgress progress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(jobId, job => job with
        {
            TotalSourceCount = progress.TotalSourceCount,
            ProcessedSourceCount = progress.ProcessedSourceCount,
            DocumentCount = progress.DocumentCount,
            ChunkCount = progress.ChunkCount,
            DocumentIds = progress.DocumentIds,
            ChunkIds = progress.ChunkIds,
            CurrentSource = progress.CurrentSource,
            UpdatedAt = progress.UpdatedAt
        });
        return Task.CompletedTask;
    }

    public Task MarkRunningAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        Update(jobId, job => job with
        {
            Status = IngestionJobStatus.Running,
            Error = null,
            StartedAt = job.StartedAt ?? now,
            UpdatedAt = now
        });
        return Task.CompletedTask;
    }

    public Task MarkSucceededAsync(string jobId, IngestionResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documentIds = result.DocumentIds ?? (string.IsNullOrWhiteSpace(result.DocumentId) ? [] : [result.DocumentId]);
        var now = DateTimeOffset.UtcNow;
        Update(jobId, job => job with
        {
            Status = IngestionJobStatus.Succeeded,
            DocumentCount = documentIds.Count,
            ChunkCount = result.ChunkCount,
            ProcessedSourceCount = job.TotalSourceCount > 0 ? job.TotalSourceCount : job.ProcessedSourceCount,
            DocumentIds = documentIds,
            ChunkIds = result.ChunkIds,
            CurrentSource = null,
            WorkerId = null,
            Error = null,
            UpdatedAt = now,
            CompletedAt = now
        });
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(string jobId, string error, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        Update(jobId, job => job with
        {
            Status = IngestionJobStatus.Failed,
            WorkerId = null,
            Error = error,
            UpdatedAt = now,
            CompletedAt = now
        });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IngestionJob>> GetRestartableJobsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IngestionJob> jobs = _jobs.Values
            .Where(job => job.Status is IngestionJobStatus.Queued or IngestionJobStatus.Running)
            .OrderBy(job => job.CreatedAt)
            .ToArray();
        return Task.FromResult(jobs);
    }

    public Task<IngestionJob?> TryAcquireAsync(string jobId, string workerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        IngestionJob? acquired = null;
        _jobs.AddOrUpdate(
            jobId,
            id => throw new KeyNotFoundException($"Ingestion job '{id}' was not found."),
            (_, existing) =>
            {
                if (existing.Status != IngestionJobStatus.Queued)
                {
                    return existing;
                }

                acquired = existing with
                {
                    Status = IngestionJobStatus.Running,
                    WorkerId = workerId,
                    Error = null,
                    StartedAt = existing.StartedAt ?? now,
                    UpdatedAt = now
                };
                return acquired;
            });
        return Task.FromResult(acquired);
    }

    private void Update(string jobId, Func<IngestionJob, IngestionJob> update)
    {
        _jobs.AddOrUpdate(
            jobId,
            id => throw new KeyNotFoundException($"Ingestion job '{id}' was not found."),
            (_, existing) => update(existing));
    }

    private static DateTimeOffset Now(DateTimeOffset value)
    {
        return value == default ? DateTimeOffset.UtcNow : value;
    }
}
