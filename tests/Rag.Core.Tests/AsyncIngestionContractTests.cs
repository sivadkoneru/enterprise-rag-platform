using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Jobs;
using Rag.Core.Models;
using Rag.Core.Sources;
using Rag.Providers.Mongo;
using Xunit;

namespace Rag.Core.Tests;

public sealed class AsyncIngestionContractTests
{
    [Fact]
    public async Task LocalDirectoryIngestionKeepsSupportedRecursiveFileBehavior()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rag-local-source-{Guid.NewGuid():N}");
        var nested = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(directory, "first.txt"), "Alpha refunds are tracked locally.");
        await File.WriteAllTextAsync(Path.Combine(nested, "second.md"), "# Beta\n\nMarkdown policies are local too.");
        await File.WriteAllTextAsync(Path.Combine(directory, "ignored.tmp"), "unsupported");

        try
        {
            using var services = new ServiceCollection()
                .AddRagPlatform(new ConfigurationBuilder().Build())
                .BuildServiceProvider();

            var result = await services.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest(directory));

            result.ChunkCount.Should().Be(2, "local file sources should recurse and include only txt, md, and pdf documents");
            result.ChunkIds.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LocalDirectorySourceEnumeratesSupportedFilesInStableOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rag-source-{Guid.NewGuid():N}");
        var nested = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "zeta.md"), "# Zeta");
        await File.WriteAllTextAsync(Path.Combine(directory, "alpha.txt"), "Alpha");
        await File.WriteAllTextAsync(Path.Combine(directory, "ignored.tmp"), "{}");

        try
        {
            var source = new LocalDirectorySource(Options.Create(new LocalSourceOptions()));

            var items = await ReadAllAsync(source, directory);

            items.Select(item => item.FileName).Should().Equal("alpha.txt", "zeta.md");
            items.Select(item => item.Origin).Should().OnlyContain(origin => origin == "file");
            items.Select(item => item.Extension).Should().Equal(".txt", ".md");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InMemoryJobStoreAndQueueTrackQueuedRunningSucceededAndFailedJobs()
    {
        var store = new InMemoryIngestionJobStore();
        var queue = new IngestionJobQueue(store);
        var request = new IngestionRequest(Sources: ["file:///tmp/handbook.txt", "s3://rag-docs/policies"]);

        var queued = await queue.EnqueueAsync(request);
        var dequeued = await FirstQueuedAsync(queue);

        dequeued.Id.Should().Be(queued.Id);
        dequeued.Status.Should().Be(IngestionJobStatus.Queued);
        dequeued.Request.SourceUris.Should().Equal("file:///tmp/handbook.txt", "s3://rag-docs/policies");

        await store.MarkRunningAsync(queued.Id);
        (await store.GetAsync(queued.Id))!.Status.Should().Be(IngestionJobStatus.Running);

        await store.MarkSucceededAsync(
            queued.Id,
            new IngestionResult(3, "recursive", ["chunk-a", "chunk-b", "chunk-c"], ["doc-a", "doc-b"]));
        var succeeded = await store.GetAsync(queued.Id);
        succeeded!.Status.Should().Be(IngestionJobStatus.Succeeded);
        succeeded.DocumentCount.Should().Be(2);
        succeeded.ChunkCount.Should().Be(3);
        succeeded.DocumentIds.Should().Equal("doc-a", "doc-b");

        var failed = await store.CreateAsync(new IngestionRequest(Path: "azureblob://rag-docs/bad.pdf"));
        await store.MarkFailedAsync(failed.Id, "download failed");

        var failedJob = await store.GetAsync(failed.Id);
        failedJob!.Status.Should().Be(IngestionJobStatus.Failed);
        failedJob.Error.Should().Be("download failed");
    }

    [Fact]
    public async Task InMemoryJobStoreTracksProgressAndPreservesCountsOnFailure()
    {
        var store = new InMemoryIngestionJobStore();
        var queued = await store.CreateAsync(new IngestionRequest(Sources: ["./first", "./second"]));

        await store.UpdateProgressAsync(
            queued.Id,
            new IngestionProgress(
                2,
                1,
                3,
                9,
                ["doc-a", "doc-b", "doc-c"],
                ["chunk-a", "chunk-b"],
                "./first",
                DateTimeOffset.UtcNow));
        await store.MarkFailedAsync(queued.Id, "vector store unavailable");

        var failed = await store.GetAsync(queued.Id);
        failed!.Status.Should().Be(IngestionJobStatus.Failed);
        failed.DocumentCount.Should().Be(3);
        failed.ChunkCount.Should().Be(9);
        failed.ProcessedSourceCount.Should().Be(1);
        failed.TotalSourceCount.Should().Be(2);
        failed.DocumentIds.Should().Equal("doc-a", "doc-b", "doc-c");
        failed.Error.Should().Be("vector store unavailable");
    }

    [Fact]
    public async Task InMemoryJobStoreReturnsRestartableJobsAndAcquiresQueuedJobs()
    {
        var store = new InMemoryIngestionJobStore();
        var queued = await store.CreateAsync(new IngestionRequest(Path: "./queued"));
        var running = await store.CreateAsync(new IngestionRequest(Path: "./running"));
        await store.MarkRunningAsync(running.Id);
        var paused = await store.CreateAsync(new IngestionRequest(Path: "./paused"));
        await store.MarkPausedAsync(paused.Id);
        var canceled = await store.CreateAsync(new IngestionRequest(Path: "./canceled"));
        await store.MarkCanceledAsync(canceled.Id);
        var failed = await store.CreateAsync(new IngestionRequest(Path: "./failed"));
        await store.MarkFailedAsync(failed.Id, "bad");

        var restartable = await store.GetRestartableJobsAsync();
        restartable.Select(job => job.Id).Should().Equal(queued.Id, running.Id);

        var acquired = await store.TryAcquireAsync(queued.Id, "worker-a");
        acquired!.Status.Should().Be(IngestionJobStatus.Running);
        acquired.WorkerId.Should().Be("worker-a");

        var secondAcquire = await store.TryAcquireAsync(queued.Id, "worker-b");
        secondAcquire.Should().BeNull("a running job should not be acquired twice");
    }

    [Fact]
    public async Task InMemoryJobStorePausesCancelsAndResumesJobs()
    {
        var store = new InMemoryIngestionJobStore();
        var job = await store.CreateAsync(new IngestionRequest(Path: "./source"));
        await store.MarkRunningAsync(job.Id);

        var paused = await store.MarkPausedAsync(job.Id);
        paused!.Status.Should().Be(IngestionJobStatus.Paused);
        paused.WorkerId.Should().BeNull();

        var resumed = await store.MarkQueuedAsync(job.Id);
        resumed!.Status.Should().Be(IngestionJobStatus.Queued);

        var canceled = await store.MarkCanceledAsync(job.Id);
        canceled!.Status.Should().Be(IngestionJobStatus.Canceled);
        canceled.CompletedAt.Should().NotBeNull();

        var afterCancelPause = await store.MarkPausedAsync(job.Id);
        afterCancelPause!.Status.Should().Be(IngestionJobStatus.Canceled, "terminal jobs should not be paused again");
    }

    [Fact]
    public async Task QueueCanRequeueExistingPersistentJobWithoutChangingId()
    {
        var store = new InMemoryIngestionJobStore();
        var queue = new IngestionJobQueue(store);
        var job = await store.CreateAsync(new IngestionRequest(Path: "./resume"));

        await queue.EnqueueExistingAsync(job);
        var dequeued = await FirstQueuedAsync(queue);

        dequeued.Id.Should().Be(job.Id);
        dequeued.Request.Path.Should().Be("./resume");
    }

    [Fact]
    public void AddRagPlatformSelectsMongoJobStoreFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JOB_STORE"] = "mongo",
                ["MONGO_CONNECTION_STRING"] = "mongodb://localhost:27017",
                ["MONGO_DATABASE"] = "rag",
                ["MONGO_JOBS_COLLECTION"] = "jobs"
            })
            .Build();

        // The Mongo job store now lives in the opt-in Rag.Providers.Mongo package; AddRagMongo
        // registers it as a keyed IIngestionJobStore that AddRagPlatform's resolver looks up by name.
        using var services = new ServiceCollection()
            .AddRagPlatform(configuration)
            .AddRagMongo(configuration)
            .BuildServiceProvider();

        services.GetRequiredService<IIngestionJobStore>().Should().BeOfType<MongoIngestionJobStore>();
    }

    [Fact]
    public async Task IngestionPipelineReportsProgressForEnumeratedSources()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rag-progress-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "first.txt"), "Alpha progress.");
        await File.WriteAllTextAsync(Path.Combine(directory, "second.txt"), "Beta progress.");

        try
        {
            using var services = new ServiceCollection()
                .AddRagPlatform(new ConfigurationBuilder().Build())
                .BuildServiceProvider();
            var progress = new CaptureProgress();

            await services.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest(directory), progress);

            progress.Values.Should().NotBeEmpty();
            progress.Values.Last().TotalSourceCount.Should().Be(2);
            progress.Values.Last().ProcessedSourceCount.Should().Be(2);
            progress.Values.Last().DocumentCount.Should().Be(2);
            progress.Values.Last().ChunkCount.Should().Be(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task IngestionPipelineStopsWhenControlStatusIsPaused()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rag-paused-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "first.txt"), "Alpha progress.");

        try
        {
            using var services = new ServiceCollection()
                .AddRagPlatform(new ConfigurationBuilder().Build())
                .BuildServiceProvider();

            var act = async () => await services.GetRequiredService<IIngestionPipeline>().IngestAsync(
                new IngestionRequest(directory),
                null,
                _ => Task.FromResult(IngestionJobStatus.Paused));

            await act.Should().ThrowAsync<IngestionJobPausedException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<IngestionJob> FirstQueuedAsync(IngestionJobQueue queue)
    {
        await foreach (var job in queue.DequeueAllAsync())
        {
            return job;
        }

        throw new InvalidOperationException("Queue completed before yielding a job.");
    }

    private static async Task<IReadOnlyList<SourceItem>> ReadAllAsync(LocalDirectorySource source, string uri)
    {
        var items = new List<SourceItem>();
        await foreach (var item in source.EnumerateAsync(uri))
        {
            items.Add(item);
        }

        return items;
    }

    private sealed class CaptureProgress : IProgress<IngestionProgress>
    {
        public List<IngestionProgress> Values { get; } = [];

        public void Report(IngestionProgress value)
        {
            Values.Add(value);
        }
    }
}
