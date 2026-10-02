using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rag.Core.Common;

namespace Rag.Core.Workbench;

public sealed class WorkbenchJobs(WorkbenchCatalog catalog, WorkbenchIngestor ingestor, DetailedQueryPipeline queries, ILogger<WorkbenchJobs> logger) : BackgroundService
{
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _changes = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<WorkbenchJob> EnqueueIngestionAsync(string profileId, WorkbenchIngestionRequest request, CancellationToken token = default)
    {
        await catalog.GetProfileAsync(profileId, token).ConfigureAwait(false);
        if (request.Sources is not { Count: > 0 and <= 100 } || request.Sources.Any(source => string.IsNullOrWhiteSpace(source) || source.Length > 4000))
        {
            throw new ArgumentException("Provide between 1 and 100 valid source paths or URIs.");
        }

        var jobs = await ListAsync(profileId, token).ConfigureAwait(false);
        if (jobs.Any(job => job.Kind == "ingestion" && job.Status is "queued" or "running" or "paused"))
        {
            throw new ArgumentException("An ingestion job is already active for this indexing profile.");
        }

        return await EnqueueAsync("ingestion", profileId, request.Sources.Count, JsonSerializer.Serialize(request, Json), token).ConfigureAwait(false);
    }
    public async Task<WorkbenchJob> EnqueueEvaluationAsync(LiveEvaluationRequest request, CancellationToken token = default)
    {
        WorkbenchEvaluation.Validate(request);
        string? corpusId = null;
        foreach (var id in request.ProfileIds)
        {
            var profile = await catalog.GetProfileAsync(id, token).ConfigureAwait(false);
            if (profile.Status != "ready")
            {
                throw new ArgumentException("Every evaluation profile must be fully indexed.");
            }
            if (corpusId is not null && corpusId != profile.CorpusId) { throw new ArgumentException("Compare profiles belonging to the same corpus."); }
            corpusId = profile.CorpusId;
            var documents = await catalog.GetDocumentsAsync(profile.Id, token).ConfigureAwait(false);
            foreach (var question in request.Questions.Where(question => !question.ExpectedAbstention))
            {
                foreach (var anchor in question.GoldAnchors)
                {
                    var matched = documents.Where(document => (question.ExpectedSourceFile is null || string.Equals(document.Filename, question.ExpectedSourceFile, StringComparison.OrdinalIgnoreCase)) && document.Content.Contains(anchor.Phrase, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (matched.Length == 0) { throw new ArgumentException($"No source resolves the evidence anchor for question '{question.Id}'."); }
                    if (matched.Length != 1) { throw new ArgumentException($"The source is ambiguous for question '{question.Id}'; choose a unique expectedSourceFile and anchor."); }
                }
            }
        }
        return await EnqueueAsync("evaluation", null, request.ProfileIds.Count * request.Questions.Count, JsonSerializer.Serialize(request, Json), token).ConfigureAwait(false);
    }
    private async Task<WorkbenchJob> EnqueueAsync(string kind, string? profileId, int total, string payload, CancellationToken token)
    {
        var id = Guid.NewGuid().ToString("N");
        var job = new WorkbenchJob(id, kind, "queued", profileId, 0, total, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, payload);
        await catalog.Store.SaveAsync("jobs", id, job, token).ConfigureAwait(false);
        if (kind == "evaluation")
        {
            await catalog.Store.SaveAsync("evaluations", id, new LiveEvaluationReport(id, job.CreatedAt, "queued", [], [], WorkbenchEvaluation.GroundednessCaveat, 5), token).ConfigureAwait(false);
        }

        await _queue.Writer.WriteAsync(id, token).ConfigureAwait(false);
        return job;
    }
    public Task<WorkbenchJob?> GetAsync(string id, CancellationToken token = default) => catalog.Store.GetAsync<WorkbenchJob>("jobs", id, token);
    public async Task<IReadOnlyList<WorkbenchJob>> ListAsync(string? profileId, CancellationToken token = default) => (await catalog.Store.ListAsync<WorkbenchJob>("jobs", token).ConfigureAwait(false)).Where(job => profileId is null || job.ProfileId == profileId).OrderByDescending(job => job.CreatedAt).ToArray();
    public async Task<WorkbenchJob> ControlAsync(string id, string action, CancellationToken token = default)
    {
        var resume = false;
        WorkbenchJob result;
        await _changes.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var job = await GetAsync(id, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Job not found.");
            var status = action switch
            {
                "pause" when job.Status is "queued" or "running" => "paused",
                "cancel" when job.Status is "queued" or "running" or "paused" => "canceled",
                "resume" when job.Status == "paused" => "queued",
                "pause" or "cancel" or "resume" => job.Status,
                _ => throw new ArgumentException("Unsupported job action.")
            };
            resume = action == "resume" && status != job.Status;
            result = job with { Status = status, UpdatedAt = DateTimeOffset.UtcNow };
            await catalog.Store.SaveAsync("jobs", id, result, token).ConfigureAwait(false);
            if (result.ProfileId is not null && result.Status is "paused" or "canceled")
            {
                var profile = await catalog.GetProfileAsync(result.ProfileId, token).ConfigureAwait(false);
                await catalog.Store.SaveAsync("profiles", profile.Id, profile with { Status = result.Status }, token).ConfigureAwait(false);
            }
            if (result.Kind == "evaluation")
            {
                var report = await catalog.Store.GetAsync<LiveEvaluationReport>("evaluations", id, token).ConfigureAwait(false);
                if (report is not null) { await catalog.Store.SaveAsync("evaluations", id, report with { Status = result.Status }, token).ConfigureAwait(false); }
            }
            if (action is "pause" or "cancel" && _active.TryGetValue(id, out var active))
            {
                active.Cancel();
            }
        }
        finally { _changes.Release(); }
        if (resume)
        {
            await _queue.Writer.WriteAsync(id, token).ConfigureAwait(false);
        }

        return result;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var recoveryRetry = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var existing = await ListAsync(null, stoppingToken).ConfigureAwait(false);
                foreach (var job in existing.Where(job => job.Status is "queued" or "running"))
                {
                    await catalog.Store.SaveAsync("jobs", job.Id, job with { Status = "queued", UpdatedAt = DateTimeOffset.UtcNow }, stoppingToken).ConfigureAwait(false);
                    await _queue.Writer.WriteAsync(job.Id, stoppingToken).ConfigureAwait(false);
                }
                break;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Workbench job recovery could not contact the configured store ({ErrorType}); retrying.", exception.GetType().Name);
                await recoveryRetry.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        await foreach (var id in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            var job = await GetAsync(id, stoppingToken).ConfigureAwait(false);
            if (job is null || job.Status != "queued")
            {
                continue;
            }

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _active[id] = cancellation;
            try
            {
                await SaveStatusAsync(id, "running", null, stoppingToken).ConfigureAwait(false);
                async Task CheckAsync(CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    var current = await GetAsync(id, token).ConfigureAwait(false);
                    if (current?.Status is "paused" or "canceled")
                    {
                        throw new OperationCanceledException(token);
                    }
                }
                if (job.Kind == "ingestion")
                {
                    var request = JsonSerializer.Deserialize<WorkbenchIngestionRequest>(job.Payload, Json) ?? throw new InvalidDataException("Missing ingestion payload.");
                    await ingestor.IngestAsync(job, request.Sources, CheckAsync, (completed, token) => ProgressAsync(job.Id, completed, token), cancellation.Token).ConfigureAwait(false);
                }
                else
                {
                    var request = JsonSerializer.Deserialize<LiveEvaluationRequest>(job.Payload, Json) ?? throw new InvalidDataException("Missing evaluation payload.");
                    await EvaluateAsync(job, request, CheckAsync, cancellation.Token).ConfigureAwait(false);
                }
                await SaveStatusAsync(id, "complete", null, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                var current = await GetAsync(id, CancellationToken.None).ConfigureAwait(false);
                if (stoppingToken.IsCancellationRequested)
                {
                    await SaveStatusAsync(id, "queued", null, CancellationToken.None).ConfigureAwait(false);
                }
                else if (current?.Status is not ("paused" or "canceled"))
                {
                    await SaveStatusAsync(id, cancellation.IsCancellationRequested ? "canceled" : "failed", cancellation.IsCancellationRequested ? null : "A provider request timed out.", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                logger.LogError("Workbench job {JobId} failed ({ErrorType}); remote details are not returned.", id, exception.GetType().Name);
                await SaveStatusAsync(id, "failed", "The job failed. Check backend connectivity and server diagnostics.", stoppingToken).ConfigureAwait(false);
                if (job.ProfileId is not null)
                {
                    var profile = await catalog.GetProfileAsync(job.ProfileId, stoppingToken).ConfigureAwait(false);
                    await catalog.Store.SaveAsync("profiles", profile.Id, profile with { Status = "failed" }, stoppingToken).ConfigureAwait(false);
                }
            }
            finally { _active.TryRemove(id, out _); }
        }
    }
    private async Task ProgressAsync(string id, int completed, CancellationToken token)
    {
        await _changes.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var current = await GetAsync(id, token).ConfigureAwait(false);
            if (current is null || current.Status is "paused" or "canceled") { return; }
            await catalog.Store.SaveAsync("jobs", id, current with { Completed = completed, UpdatedAt = DateTimeOffset.UtcNow }, token).ConfigureAwait(false);
        }
        finally { _changes.Release(); }
    }
    private async Task SaveStatusAsync(string id, string status, string? error, CancellationToken token)
    {
        await _changes.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var job = await GetAsync(id, token).ConfigureAwait(false);
            if (job is null)
            {
                return;
            }

            if (status is "running" or "complete" && job.Status is "paused" or "canceled")
            {
                return;
            }

            await catalog.Store.SaveAsync("jobs", id, job with { Status = status, Error = error, Completed = status == "complete" ? job.Total : job.Completed, UpdatedAt = DateTimeOffset.UtcNow }, token).ConfigureAwait(false);
            if (job.Kind == "evaluation")
            {
                var report = await catalog.Store.GetAsync<LiveEvaluationReport>("evaluations", id, token).ConfigureAwait(false);
                if (report is not null)
                {
                    await catalog.Store.SaveAsync("evaluations", id, report with { Status = status }, token).ConfigureAwait(false);
                }
            }
        }
        finally { _changes.Release(); }
    }
    private async Task EvaluateAsync(WorkbenchJob job, LiveEvaluationRequest request, Func<CancellationToken, Task> check, CancellationToken token)
    {
        var previous = await catalog.Store.GetAsync<LiveEvaluationReport>("evaluations", job.Id, token).ConfigureAwait(false);
        var profiles = previous?.Profiles.ToList() ?? [];
        var completed = profiles.Sum(profile => profile.Outcomes.Count);
        foreach (var id in request.ProfileIds)
        {
            var profile = await catalog.GetProfileAsync(id, token).ConfigureAwait(false);
            var existing = profiles.FirstOrDefault(item => item.ProfileId == id);
            var outcomes = existing?.Outcomes.ToList() ?? [];
            foreach (var question in request.Questions)
            {
                await check(token).ConfigureAwait(false);
                if (outcomes.Any(outcome => outcome.QuestionId == question.Id))
                {
                    continue;
                }

                var run = await queries.QueryAsync(new DetailedQueryRequest(question.Question, profile.CorpusId, profile.Id, request.TopK, request.Mode, request.Reranker, request.MinRelevance, request.Neighbors, request.MaxContextTokens), token: token).ConfigureAwait(false);
                outcomes.Add(WorkbenchEvaluation.Score(question, run));
                completed++;
                var result = new LiveProfileEvaluation(profile.Id, profile.Name, WorkbenchEvaluation.Aggregate(outcomes), profile.EmbeddingOperations, outcomes.Average(outcome => outcome.Run.ContextTokens), outcomes.Average(outcome => outcome.Run.TotalLatencyMs), outcomes.ToArray()) { ProfileSnapshot = existing?.ProfileSnapshot ?? profile, SourceRevision = existing?.SourceRevision ?? StableId.Compute(string.Join("\n", (await catalog.GetDocumentsAsync(profile.Id, token).ConfigureAwait(false)).OrderBy(document => document.Id).Select(document => $"{document.Id}:{document.Content}"))) };
                profiles.RemoveAll(item => item.ProfileId == id);
                profiles.Add(result);
                await catalog.Store.SaveAsync("evaluations", job.Id, new LiveEvaluationReport(job.Id, job.CreatedAt, "running", request.Questions, profiles.ToArray(), WorkbenchEvaluation.GroundednessCaveat, request.TopK) { QuerySettings = request }, token).ConfigureAwait(false);
                await ProgressAsync(job.Id, completed, token).ConfigureAwait(false);
            }
        }
    }
    public override void Dispose()
    {
        foreach (var active in _active.Values)
        {
            active.Cancel();
        }

        _changes.Dispose();
        base.Dispose();
    }
}
