using FluentAssertions;
using Rag.Core.Abstractions;
using Rag.Core.Jobs;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

/// <summary>
/// Conformance suite for <see cref="IIngestionJobStore"/> implementations. Concrete stores (in-memory
/// here, MongoDB in the integration test project) subclass this and provide a fresh store per test via
/// <see cref="CreateStore"/>, so every implementation is held to the same transition semantics.
/// </summary>
public abstract class IngestionJobStoreContractTests
{
    protected abstract IIngestionJobStore CreateStore();

    [Fact]
    public async Task CreateYieldsQueuedStatusWithTimestamps()
    {
        var store = CreateStore();

        var job = await store.CreateAsync(new IngestionRequest(Path: "./source"));

        job.Status.Should().Be(IngestionJobStatus.Queued);
        job.CreatedAt.Should().NotBe(default);
        job.UpdatedAt.Should().NotBe(default);
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task TryAcquireTransitionsQueuedToRunningAndReturnsNullOnSecondAttempt()
    {
        var store = CreateStore();
        var job = await store.CreateAsync(new IngestionRequest(Path: "./source"));

        var acquired = await store.TryAcquireAsync(job.Id, "worker-a");
        var secondAttempt = await store.TryAcquireAsync(job.Id, "worker-b");

        acquired.Should().NotBeNull();
        acquired!.Status.Should().Be(IngestionJobStatus.Running);
        acquired.WorkerId.Should().Be("worker-a");
        acquired.StartedAt.Should().NotBeNull();
        secondAttempt.Should().BeNull("a job already acquired by another worker should not be acquired twice");
    }

    [Fact]
    public async Task MarkRunningPreservesAnExistingStartedAt()
    {
        var store = CreateStore();
        var job = await store.CreateAsync(new IngestionRequest(Path: "./source"));
        var acquired = await store.TryAcquireAsync(job.Id, "worker-a");
        var originalStartedAt = acquired!.StartedAt;
        originalStartedAt.Should().NotBeNull();

        await Task.Delay(15);
        await store.MarkRunningAsync(job.Id);

        var reloaded = await store.GetAsync(job.Id);
        reloaded!.Status.Should().Be(IngestionJobStatus.Running);
        reloaded.StartedAt.Should().Be(originalStartedAt, "re-marking a job running must not overwrite its original start time");
    }

    [Fact]
    public async Task MarkSucceededBackfillsProcessedSourceCountAndRecordsResultDocumentIds()
    {
        var store = CreateStore();
        var job = await store.CreateAsync(new IngestionRequest(Sources: ["./a", "./b"]));
        await store.TryAcquireAsync(job.Id, "worker-a");
        await store.UpdateProgressAsync(
            job.Id,
            new IngestionProgress(
                TotalSourceCount: 2,
                ProcessedSourceCount: 1,
                DocumentCount: 1,
                ChunkCount: 4,
                DocumentIds: ["doc-a"],
                ChunkIds: ["chunk-a", "chunk-b", "chunk-c", "chunk-d"],
                CurrentSource: "./b",
                UpdatedAt: DateTimeOffset.UtcNow));

        await store.MarkSucceededAsync(job.Id, new IngestionResult(5, "fixed", ["chunk-x"], ["doc-final"]));

        var succeeded = await store.GetAsync(job.Id);
        succeeded!.Status.Should().Be(IngestionJobStatus.Succeeded);
        succeeded.ProcessedSourceCount.Should().Be(
            2,
            "ProcessedSourceCount should be backfilled from TotalSourceCount when the final result did not account for every source individually");
        // Equal(params string[]) would read the reason as a second expected element, so the
        // expectation is passed as an explicit collection.
        succeeded.DocumentIds.Should().Equal(
            ["doc-final"],
            "the document ids recorded on the job come straight from the ingestion result");
        succeeded.DocumentCount.Should().Be(1);
    }

    [Fact]
    public async Task PauseAndCancelAreNoOpsOnTerminalJobs()
    {
        var store = CreateStore();
        var job = await store.CreateAsync(new IngestionRequest(Path: "./source"));
        await store.MarkFailedAsync(job.Id, "boom");

        var pauseResult = await store.MarkPausedAsync(job.Id);
        var cancelResult = await store.MarkCanceledAsync(job.Id);

        pauseResult!.Status.Should().Be(IngestionJobStatus.Failed, "a terminal job should not transition to paused");
        pauseResult.Error.Should().Be("boom");
        cancelResult!.Status.Should().Be(IngestionJobStatus.Failed, "a terminal job should not transition to canceled");
    }

    [Fact]
    public async Task MarkQueuedOnlyResurrectsAPausedJob()
    {
        var store = CreateStore();
        var runningJob = await store.CreateAsync(new IngestionRequest(Path: "./running"));
        await store.TryAcquireAsync(runningJob.Id, "worker-a");

        var notPausedResult = await store.MarkQueuedAsync(runningJob.Id);
        notPausedResult!.Status.Should().Be(IngestionJobStatus.Running, "MarkQueued should be a no-op for jobs that are not paused");

        var pausableJob = await store.CreateAsync(new IngestionRequest(Path: "./pausable"));
        await store.TryAcquireAsync(pausableJob.Id, "worker-b");
        await store.MarkPausedAsync(pausableJob.Id);

        var resurrected = await store.MarkQueuedAsync(pausableJob.Id);
        resurrected!.Status.Should().Be(IngestionJobStatus.Queued);
        resurrected.StartedAt.Should().BeNull();
        resurrected.WorkerId.Should().BeNull();
    }

    [Fact]
    public async Task GetRestartableJobsReturnsQueuedAndRunningOrderedByCreatedAt()
    {
        var store = CreateStore();
        var first = await store.CreateAsync(new IngestionRequest(Path: "./first"));
        var second = await store.CreateAsync(new IngestionRequest(Path: "./second"));
        await store.TryAcquireAsync(second.Id, "worker-a");
        var third = await store.CreateAsync(new IngestionRequest(Path: "./third"));
        await store.MarkFailedAsync(third.Id, "not restartable");
        var fourth = await store.CreateAsync(new IngestionRequest(Path: "./fourth"));

        var restartable = await store.GetRestartableJobsAsync();

        restartable.Select(job => job.Id).Should().Equal(first.Id, second.Id, fourth.Id);
    }
}

public sealed class InMemoryIngestionJobStoreContractTests : IngestionJobStoreContractTests
{
    protected override IIngestionJobStore CreateStore()
    {
        return new InMemoryIngestionJobStore();
    }
}
