using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using Rag.Core.Vector;
using Xunit;

namespace Rag.Integration.Tests;

public sealed class BackendContainerTests
{
    [Fact]
    public async Task MongoDocumentStorePersistsAndHydratesChunks()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage("mongo:7")
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017))
            .Build();
        await mongo.StartAsync();

        var connectionString = $"mongodb://localhost:{mongo.GetMappedPublicPort(27017)}";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DOC_STORE"] = "mongo",
                ["MONGO_CONNECTION_STRING"] = connectionString,
                ["MONGO_DATABASE"] = $"rag_{Guid.NewGuid():N}"
            })
            .Build();
        var store = new ServiceCollection().AddRagPlatform(config).BuildServiceProvider().GetRequiredService<IDocumentStore>();
        var metadata = new DocumentMetadata("doc", "container", "doc.txt", "txt", "text/plain", 10, DateTimeOffset.UtcNow);
        var document = new ParsedDocument("doc", "hello world", metadata);
        var chunk = new TextChunk("chunk-1", "doc", 0, "hello world", 0, 11, metadata);

        await store.UpsertDocumentAsync(document);
        await store.UpsertChunksAsync([chunk]);
        var hydrated = await store.GetChunksAsync(["chunk-1"]);

        hydrated.Should().ContainSingle(item => item.Id == "chunk-1" && item.Text == "hello world");
    }

    [Fact]
    public async Task ElasticsearchVectorStoreIndexesAndSearchesByCosineSimilarity()
    {
        await using var elasticsearch = new ContainerBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.15.0")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithPortBinding(9200, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9200).ForPath("/")))
            .Build();
        await elasticsearch.StartAsync();

        var endpoint = $"http://localhost:{elasticsearch.GetMappedPublicPort(9200)}";
        var store = new ElasticsearchVectorStore(
            new SimpleHttpClientFactory(),
            Options.Create(new VectorStoreOptions
            {
                Provider = "elasticsearch",
                Endpoint = endpoint,
                IndexName = $"rag-{Guid.NewGuid():N}",
                Dimensions = 3
            }));

        await store.EnsureIndexAsync();
        await store.UpsertAsync(
            [
                new VectorRecord("chunk-a", "doc", [1, 0, 0], new Dictionary<string, string>()),
                new VectorRecord("chunk-b", "doc", [0, 1, 0], new Dictionary<string, string>())
            ]);
        var results = await store.SearchAsync([1, 0, 0], 1);

        results.Should().ContainSingle();
        results[0].ChunkId.Should().Be("chunk-a");
    }

    private sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
