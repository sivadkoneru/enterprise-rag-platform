using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Core.Jobs;
using Rag.Core.Models;
using Rag.Core.Sources;
using Xunit;

namespace Rag.Core.Tests;

public sealed class AsyncIngestionContractTests
{
    [Fact]
    public void SourceContractsArePublicAndRegistered()
    {
        var resolverType = RequiredType("Rag.Core.Abstractions.IDocumentSourceResolver");
        RequiredType("Rag.Core.Abstractions.IDocumentSource");
        RequiredType("Rag.Core.Models.SourceItem");
        FindType(
                "Rag.Core.Sources.LocalDirectorySource",
                "Rag.Core.Pipelines.LocalDirectorySource")
            .Should()
            .NotBeNull("file sources should be resolved by a concrete local directory adapter");

        using var services = new ServiceCollection()
            .AddRagPlatform(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        services.GetService(resolverType).Should().NotBeNull("AddRagPlatform must register source resolution for file, s3, and azureblob");
    }

    [Fact]
    public void IngestionRequestSupportsMultipleSourceUrisWithoutBreakingPathCompatibility()
    {
        typeof(IngestionRequest).GetProperty("Path")
            .Should()
            .NotBeNull("existing IngestionRequest(Path, Strategy) call sites must keep working");
        typeof(IngestionRequest).GetProperty("Sources")
            .Should()
            .NotBeNull("async ingestion should accept multiple file, s3, and azureblob source URIs");
    }

    [Fact]
    public void JobStoreAndQueueContractsExposeAsyncStatusTracking()
    {
        var jobType = RequiredType("Rag.Core.Models.IngestionJob");
        var statusType = RequiredType("Rag.Core.Models.IngestionJobStatus");
        var storeType = RequiredType("Rag.Core.Abstractions.IIngestionJobStore");
        var queueType = FindType(
            "Rag.Core.Abstractions.IIngestionJobQueue",
            "Rag.Core.Pipelines.IngestionJobQueue");

        statusType.IsEnum.Should().BeTrue("job status should be a closed set of states");
        Enum.GetNames(statusType).Should().Contain(["Queued", "Running", "Succeeded", "Failed"]);
        jobType.GetProperty("Status").Should().NotBeNull();
        jobType.GetProperty("DocumentCount").Should().NotBeNull();
        jobType.GetProperty("ChunkCount").Should().NotBeNull();
        jobType.GetProperty("TotalSourceCount").Should().NotBeNull();
        jobType.GetProperty("ProcessedSourceCount").Should().NotBeNull();
        jobType.GetProperty("CurrentSource").Should().NotBeNull();
        jobType.GetProperty("UpdatedAt").Should().NotBeNull();
        jobType.GetProperty("DocumentIds").Should().NotBeNull();
        jobType.GetProperty("Error").Should().NotBeNull();
        storeType.GetMethods().Should().Contain(method => method.Name.Contains("Get", StringComparison.OrdinalIgnoreCase));
        storeType.GetMethods().Should().Contain(method => method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));
        storeType.GetMethods().Should().Contain(method => method.Name == "UpdateProgressAsync");
        storeType.GetMethods().Should().Contain(method => method.Name == "GetRestartableJobsAsync");
        storeType.GetMethods().Should().Contain(method => method.Name == "TryAcquireAsync");
        queueType.Should().NotBeNull("the background Channel queue should have a public registration point for API/CLI orchestration");
    }

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
            var source = new LocalDirectorySource();

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
            new IngestionResult("doc-a", 3, "recursive", ["chunk-a", "chunk-b", "chunk-c"], ["doc-a", "doc-b"]));
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

        using var services = new ServiceCollection()
            .AddRagPlatform(configuration)
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

    private static Type RequiredType(string fullName)
    {
        var type = typeof(IngestionRequest).Assembly.GetType(fullName);
        type.Should().NotBeNull($"the async ingestion plan requires public contract {fullName}");
        return type!;
    }

    private static Type? FindType(params string[] fullNames)
    {
        var assembly = typeof(IngestionRequest).Assembly;
        return fullNames.Select(assembly.GetType).FirstOrDefault(type => type is not null);
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
