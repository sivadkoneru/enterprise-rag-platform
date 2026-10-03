using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Vector;
using Rag.Core.Workbench;
using Rag.Providers.Mongo.Stores;
using Xunit;

namespace Rag.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class WorkbenchPersistenceTests
{
    [Fact]
    public async Task MongoCatalogProfilesJobsAndReportsPersistAcrossAdapterInstances()
    {
        await using var mongo = await DockerPrerequisite.StartAsync(() => new ContainerBuilder().WithImage("mongo:8.0.32").WithPortBinding(27017, true).WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017)).Build());
        var options = Options.Create(new DocumentStoreOptions { Provider = "mongo", ConnectionString = $"mongodb://localhost:{mongo.GetMappedPublicPort(27017)}", DatabaseName = $"workbench_{Guid.NewGuid():N}" });
        var first = new MongoWorkbenchStateStore(options);
        var corpus = new WorkbenchCorpus("corpus", "Actual corpus", "Persistent", DateTimeOffset.UtcNow);
        var profile = new IndexProfile("profile", corpus.Id, "Main", "recursive", 800, 120, "configured-model", 3, "ready", 1, 1, DateTimeOffset.UtcNow, ["doc"], ["chunk"]);
        var job = new WorkbenchJob("job", "evaluation", "complete", profile.Id, 1, 1, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "{}");
        var report = new LiveEvaluationReport(job.Id, job.CreatedAt, "complete", [], [], WorkbenchEvaluation.GroundednessCaveat, 5);
        await first.SaveAsync("corpora", corpus.Id, corpus);
        await first.SaveAsync("profiles", profile.Id, profile);
        await first.SaveAsync("jobs", job.Id, job);
        await first.SaveAsync("evaluations", report.Id, report);
        var second = new MongoWorkbenchStateStore(options);
        (await second.GetAsync<WorkbenchCorpus>("corpora", corpus.Id)).Should().BeEquivalentTo(corpus);
        (await second.GetAsync<IndexProfile>("profiles", profile.Id)).Should().BeEquivalentTo(profile);
        (await second.GetAsync<WorkbenchJob>("jobs", job.Id)).Should().BeEquivalentTo(job);
        (await second.GetAsync<LiveEvaluationReport>("evaluations", report.Id)).Should().BeEquivalentTo(report);
        (await second.ListAsync<IndexProfile>("profiles")).Should().ContainSingle();
    }

    [Fact]
    public async Task ElasticsearchLexicalSearchUsesIndexedTextAndIsolatedDocumentFilters()
    {
        await using var elasticsearch = await DockerPrerequisite.StartAsync(() => new ContainerBuilder().WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.15.0").WithEnvironment("discovery.type", "single-node").WithEnvironment("xpack.security.enabled", "false").WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m").WithPortBinding(9200, true).WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9200).ForPath("/"))).Build());
        var store = new ElasticsearchVectorStore(new Factory(), Options.Create(new VectorStoreOptions { Provider = "elasticsearch", Endpoint = $"http://localhost:{elasticsearch.GetMappedPublicPort(9200)}", IndexName = $"workbench-{Guid.NewGuid():N}", Dimensions = 3 }), NullLogger<ElasticsearchVectorStore>.Instance);
        await store.EnsureIndexAsync();
        await store.UpsertAsync([new VectorRecord("refund", "allowed", [1, 0, 0], new Dictionary<string, string> { ["text"] = "Refund eligibility is thirty days." }), new VectorRecord("api", "allowed", [0, 1, 0], new Dictionary<string, string> { ["text"] = "API access requires authentication." }), new VectorRecord("foreign", "foreign", [1, 0, 0], new Dictionary<string, string> { ["text"] = "Refund refund refund eligibility." })]);
        var result = await store.SearchLexicalAsync("refund eligibility", 5, new VectorSearchFilter(DocumentIds: ["allowed"]));
        result.Should().ContainSingle().Which.ChunkId.Should().Be("refund");
        result[0].Score.Should().BeGreaterThan(0);
    }
    private sealed class Factory : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }
}
