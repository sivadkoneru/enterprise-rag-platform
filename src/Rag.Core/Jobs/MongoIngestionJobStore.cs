using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Jobs;

public sealed class MongoIngestionJobStore : IIngestionJobStore
{
    private readonly IMongoCollection<MongoIngestionJobDocument> _jobs;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public MongoIngestionJobStore(IOptions<JobStoreOptions> options)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            throw new InvalidOperationException("JobStore:ConnectionString or MONGO_CONNECTION_STRING is required for MongoDB job storage.");
        }

        var client = new MongoClient(config.ConnectionString);
        var database = client.GetDatabase(config.DatabaseName);
        _jobs = database.GetCollection<MongoIngestionJobDocument>(config.CollectionName);
    }

    public async Task<IngestionJob> CreateAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var job = new IngestionJob(
            Guid.NewGuid().ToString("N"),
            request,
            IngestionJobStatus.Queued,
            CreatedAt: now,
            UpdatedAt: now);
        await _jobs.InsertOneAsync(ToDocument(job), cancellationToken: cancellationToken).ConfigureAwait(false);
        return job;
    }

    public async Task<IngestionJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var document = await _jobs.Find(item => item.Id == jobId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?.ToJob();
    }

    public async Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var normalized = job with { UpdatedAt = job.UpdatedAt == default ? DateTimeOffset.UtcNow : job.UpdatedAt };
        await _jobs.ReplaceOneAsync(
            item => item.Id == normalized.Id,
            ToDocument(normalized),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateProgressAsync(string jobId, IngestionProgress progress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var update = Builders<MongoIngestionJobDocument>.Update
            .Set(item => item.TotalSourceCount, progress.TotalSourceCount)
            .Set(item => item.ProcessedSourceCount, progress.ProcessedSourceCount)
            .Set(item => item.DocumentCount, progress.DocumentCount)
            .Set(item => item.ChunkCount, progress.ChunkCount)
            .Set(item => item.DocumentIds, progress.DocumentIds.ToList())
            .Set(item => item.ChunkIds, progress.ChunkIds.ToList())
            .Set(item => item.CurrentSource, progress.CurrentSource)
            .Set(item => item.UpdatedAt, progress.UpdatedAt);
        await _jobs.UpdateOneAsync(item => item.Id == jobId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkRunningAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var update = Builders<MongoIngestionJobDocument>.Update
            .Set(item => item.Status, IngestionJobStatus.Running.ToString())
            .Set(item => item.Error, null)
            .Set(item => item.UpdatedAt, now)
            .Set(item => item.StartedAt, now);
        await _jobs.UpdateOneAsync(item => item.Id == jobId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkSucceededAsync(string jobId, IngestionResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var documentIds = result.DocumentIds ?? (string.IsNullOrWhiteSpace(result.DocumentId) ? [] : [result.DocumentId]);
        var update = Builders<MongoIngestionJobDocument>.Update
            .Set(item => item.Status, IngestionJobStatus.Succeeded.ToString())
            .Set(item => item.DocumentCount, documentIds.Count)
            .Set(item => item.ChunkCount, result.ChunkCount)
            .Set(item => item.DocumentIds, documentIds.ToList())
            .Set(item => item.ChunkIds, result.ChunkIds.ToList())
            .Set(item => item.CurrentSource, null)
            .Set(item => item.WorkerId, null)
            .Set(item => item.Error, null)
            .Set(item => item.UpdatedAt, now)
            .Set(item => item.CompletedAt, now);
        await _jobs.UpdateOneAsync(item => item.Id == jobId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(string jobId, string error, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var update = Builders<MongoIngestionJobDocument>.Update
            .Set(item => item.Status, IngestionJobStatus.Failed.ToString())
            .Set(item => item.WorkerId, null)
            .Set(item => item.Error, error)
            .Set(item => item.UpdatedAt, now)
            .Set(item => item.CompletedAt, now);
        await _jobs.UpdateOneAsync(item => item.Id == jobId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IngestionJob>> GetRestartableJobsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var statuses = new[] { IngestionJobStatus.Queued.ToString(), IngestionJobStatus.Running.ToString() };
        var filter = Builders<MongoIngestionJobDocument>.Filter.In(item => item.Status, statuses);
        var documents = await _jobs
            .Find(filter)
            .SortBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return documents.Select(document => document.ToJob()).ToArray();
    }

    public async Task<IngestionJob?> TryAcquireAsync(string jobId, string workerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<MongoIngestionJobDocument>.Filter.And(
            Builders<MongoIngestionJobDocument>.Filter.Eq(item => item.Id, jobId),
            Builders<MongoIngestionJobDocument>.Filter.Eq(item => item.Status, IngestionJobStatus.Queued.ToString()));
        var update = Builders<MongoIngestionJobDocument>.Update
            .Set(item => item.Status, IngestionJobStatus.Running.ToString())
            .Set(item => item.WorkerId, workerId)
            .Set(item => item.Error, null)
            .Set(item => item.UpdatedAt, now)
            .Set(item => item.StartedAt, now);
        var acquired = await _jobs.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<MongoIngestionJobDocument>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken).ConfigureAwait(false);
        return acquired?.ToJob();
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady)
        {
            return;
        }

        await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_indexesReady)
            {
                return;
            }

            var indexes = new[]
            {
                new CreateIndexModel<MongoIngestionJobDocument>(
                    Builders<MongoIngestionJobDocument>.IndexKeys.Ascending(item => item.Status)),
                new CreateIndexModel<MongoIngestionJobDocument>(
                    Builders<MongoIngestionJobDocument>.IndexKeys.Ascending(item => item.CreatedAt)),
                new CreateIndexModel<MongoIngestionJobDocument>(
                    Builders<MongoIngestionJobDocument>.IndexKeys.Ascending(item => item.UpdatedAt))
            };
            await _jobs.Indexes.CreateManyAsync(indexes, cancellationToken).ConfigureAwait(false);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static MongoIngestionJobDocument ToDocument(IngestionJob job)
    {
        return new MongoIngestionJobDocument
        {
            Id = job.Id,
            Path = job.Request.Path,
            Strategy = job.Request.Strategy,
            Sources = job.Request.Sources?.ToList(),
            Status = job.Status.ToString(),
            DocumentCount = job.DocumentCount,
            ChunkCount = job.ChunkCount,
            TotalSourceCount = job.TotalSourceCount,
            ProcessedSourceCount = job.ProcessedSourceCount,
            DocumentIds = job.DocumentIds?.ToList(),
            ChunkIds = job.ChunkIds?.ToList(),
            CurrentSource = job.CurrentSource,
            WorkerId = job.WorkerId,
            Error = job.Error,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt
        };
    }

    private sealed class MongoIngestionJobDocument
    {
        public string Id { get; set; } = string.Empty;

        public string? Path { get; set; }

        public string? Strategy { get; set; }

        public List<string>? Sources { get; set; }

        public string Status { get; set; } = IngestionJobStatus.Queued.ToString();

        public int DocumentCount { get; set; }

        public int ChunkCount { get; set; }

        public int TotalSourceCount { get; set; }

        public int ProcessedSourceCount { get; set; }

        public List<string>? DocumentIds { get; set; }

        public List<string>? ChunkIds { get; set; }

        public string? CurrentSource { get; set; }

        public string? WorkerId { get; set; }

        public string? Error { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public IngestionJob ToJob()
        {
            return new IngestionJob(
                Id,
                new IngestionRequest(Path, Strategy, Sources),
                Enum.TryParse<IngestionJobStatus>(Status, ignoreCase: true, out var parsedStatus)
                    ? parsedStatus
                    : IngestionJobStatus.Queued,
                DocumentCount,
                ChunkCount,
                TotalSourceCount,
                ProcessedSourceCount,
                DocumentIds,
                ChunkIds,
                CurrentSource,
                WorkerId,
                Error,
                CreatedAt,
                UpdatedAt,
                StartedAt,
                CompletedAt);
        }
    }
}
