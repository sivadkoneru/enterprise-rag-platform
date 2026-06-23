using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Core.Stores;
using Rag.Core.Vector;
using Xunit;

namespace Rag.Integration.Tests;

public sealed class AdapterSelectionTests
{
    [Theory]
    [InlineData("mongo", typeof(MongoDocumentStore))]
    [InlineData("cosmos", typeof(CosmosDocumentStore))]
    public void DocumentStoreSelectionUsesConfiguredProvider(string provider, Type expectedType)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentStore:Provider"] = provider })
            .Build();

        var store = new ServiceCollection().AddRagPlatform(config).BuildServiceProvider().GetRequiredService<IDocumentStore>();

        store.Should().BeOfType(expectedType);
    }

    [Fact]
    public void VectorStoreSelectionUsesElasticsearchAdapter()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["VectorStore:Provider"] = "elasticsearch" })
            .Build();

        var store = new ServiceCollection().AddRagPlatform(config).BuildServiceProvider().GetRequiredService<IVectorStore>();

        store.Should().BeOfType<ElasticsearchVectorStore>();
    }
}
