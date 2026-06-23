using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Xunit;

namespace Rag.Core.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void AddRagPlatformBindsHierarchicalJsonConfiguration()
    {
        const string json = """
            {
              "Rag": {
                "ChunkingStrategy": "markdown"
              },
              "Chunking": {
                "Size": 321,
                "Overlap": 32,
                "SemanticDistanceThreshold": 0.44,
                "SemanticRefineWithChat": true
              },
              "Llm": {
                "Provider": "openai",
                "EmbeddingModel": "embed-json",
                "ChatModel": "chat-json"
              },
              "DocumentStore": {
                "Provider": "mongo",
                "ConnectionString": "mongodb://json",
                "DatabaseName": "jsondb",
                "ContainerName": "jsonchunks"
              },
              "VectorStore": {
                "Provider": "elasticsearch",
                "Endpoint": "http://json-elastic:9200",
                "IndexName": "json-index",
                "Dimensions": 42
              }
            }
            """;

        using var services = BuildServicesFromJson(json);

        services.GetRequiredService<IOptions<RagOptions>>().Value.ChunkingStrategy.Should().Be("markdown");
        services.GetRequiredService<IOptions<ChunkingOptions>>().Value.Size.Should().Be(321);
        services.GetRequiredService<IOptions<ChunkingOptions>>().Value.Overlap.Should().Be(32);
        services.GetRequiredService<IOptions<ChunkingOptions>>().Value.SemanticDistanceThreshold.Should().Be(0.44);
        services.GetRequiredService<IOptions<ChunkingOptions>>().Value.SemanticRefineWithChat.Should().BeTrue();
        services.GetRequiredService<IOptions<LlmOptions>>().Value.Provider.Should().Be("openai");
        services.GetRequiredService<IOptions<LlmOptions>>().Value.EmbeddingModel.Should().Be("embed-json");
        services.GetRequiredService<IOptions<LlmOptions>>().Value.ChatModel.Should().Be("chat-json");
        services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.Provider.Should().Be("mongo");
        services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.ConnectionString.Should().Be("mongodb://json");
        services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.DatabaseName.Should().Be("jsondb");
        services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.ContainerName.Should().Be("jsonchunks");
        services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Provider.Should().Be("elasticsearch");
        services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Endpoint.Should().Be("http://json-elastic:9200");
        services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.IndexName.Should().Be("json-index");
        services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Dimensions.Should().Be(42);
    }

    [Fact]
    public void AddRagPlatformLetsEnvironmentStyleKeysOverrideJsonSections()
    {
        const string json = """
            {
              "Rag": {
                "ChunkingStrategy": "fixed"
              },
              "Llm": {
                "Provider": "deterministic",
                "EmbeddingModel": "embed-json"
              },
              "DocumentStore": {
                "Provider": "memory",
                "ConnectionString": "mongodb://json"
              },
              "VectorStore": {
                "Provider": "memory",
                "Endpoint": "http://json-elastic:9200",
                "Dimensions": 10
              }
            }
            """;
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CHUNKING_STRATEGY"] = "semantic",
                ["LLM_PROVIDER"] = "openai",
                ["OPENAI_EMBEDDING_MODEL"] = "embed-env",
                ["DOC_STORE"] = "mongo",
                ["MONGO_CONNECTION_STRING"] = "mongodb://env",
                ["VECTOR_STORE"] = "elasticsearch",
                ["ELASTICSEARCH_URI"] = "http://env-elastic:9200",
                ["ELASTICSEARCH_VECTOR_DIMENSIONS"] = "99"
            })
            .Build();

        using var services = new ServiceCollection()
            .AddRagPlatform(configuration)
            .BuildServiceProvider();

        services.GetRequiredService<IOptions<RagOptions>>().Value.ChunkingStrategy.Should().Be("semantic");
        services.GetRequiredService<IOptions<LlmOptions>>().Value.Provider.Should().Be("openai");
        services.GetRequiredService<IOptions<LlmOptions>>().Value.EmbeddingModel.Should().Be("embed-env");
        services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.Provider.Should().Be("mongo");
        services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.ConnectionString.Should().Be("mongodb://env");
        services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Provider.Should().Be("elasticsearch");
        services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Endpoint.Should().Be("http://env-elastic:9200");
        services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Dimensions.Should().Be(99);
    }

    private static ServiceProvider BuildServicesFromJson(string json)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        return new ServiceCollection()
            .AddRagPlatform(configuration)
            .BuildServiceProvider();
    }
}
