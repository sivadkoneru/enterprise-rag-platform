using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using Rag.Core.Sources;
using Rag.Providers.AzureBlob;
using Xunit;

namespace Rag.Integration.Tests;

[Collection(CloudSourceCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AzureBlobDocumentSourceTests(AzuriteFixture azurite) : IClassFixture<AzuriteFixture>
{
    private const string Schema = """
        {
          "version": 1,
          "profiles": [
            {
              "files": ["*.csv"],
              "format": "csv",
              "id": "id",
              "text": [{ "column": "content", "required": true }]
            }
          ]
        }
        """;

    [Fact]
    public async Task SupportedBlobsAreMaterializedInStableOrderAndUnsupportedOnesSkipped()
    {
        var container = await azurite.SeedContainerAsync(new Dictionary<string, string>
        {
            ["docs/handbook.txt"] = "Refunds are available within thirty days.",
            ["docs/policies.md"] = "# Policies\n\nShipping takes five days.",
            ["docs/notes.tmp"] = "unsupported",
            ["other/excluded.txt"] = "outside the prefix"
        });

        var items = await ReadAllAsync(Source(), $"azureblob://{container}/docs/");

        try
        {
            items.Select(item => item.FileName).Should().Equal("handbook.txt", "policies.md");
            items.Select(item => item.Origin).Should().OnlyContain(origin => origin == "azureblob");
            items.Select(item => item.Source).Should().Equal(
                $"azureblob://{container}/docs/handbook.txt",
                $"azureblob://{container}/docs/policies.md");
            items.Select(item => item.Extension).Should().Equal(".txt", ".md");

            (await File.ReadAllTextAsync(items[0].LocalPath)).Should().Be("Refunds are available within thirty days.");
            items[0].Attributes.Should().ContainKey("container").WhoseValue.Should().Be(container);
            items[0].Attributes.Should().ContainKey("blob").WhoseValue.Should().Be("docs/handbook.txt");
        }
        finally
        {
            await DisposeAllAsync(items);
        }
    }

    [Fact]
    public async Task SchemaSidecarsAreDownloadedAsAttributesAndNeverIngestedAsDocuments()
    {
        var container = await azurite.SeedContainerAsync(new Dictionary<string, string>
        {
            ["docs/records.csv"] = "id,content\n1,Alpha record.\n",
            ["docs/records.csv.schema.json"] = Schema
        });

        var items = await ReadAllAsync(Source(), $"azureblob://{container}/docs/");

        try
        {
            items.Should().ContainSingle("the schema sidecar is ingestion metadata, not a document");
            items[0].FileName.Should().Be("records.csv");

            items[0].Attributes.Should().ContainKey("schemaPath");
            var schemaPath = items[0].Attributes!["schemaPath"];
            File.Exists(schemaPath).Should().BeTrue("the sidecar must be materialized next to the record file");
            (await File.ReadAllTextAsync(schemaPath)).Should().Contain("\"format\": \"csv\"");
        }
        finally
        {
            await DisposeAllAsync(items);
        }
    }

    [Fact]
    public async Task DisposingASourceItemRemovesItsTemporaryFiles()
    {
        var container = await azurite.SeedContainerAsync(new Dictionary<string, string>
        {
            ["docs/records.csv"] = "id,content\n1,Alpha record.\n",
            ["docs/records.csv.schema.json"] = Schema
        });

        var items = await ReadAllAsync(Source(), $"azureblob://{container}/docs/");
        var localPath = items[0].LocalPath;
        var schemaPath = items[0].Attributes!["schemaPath"];

        await DisposeAllAsync(items);

        File.Exists(localPath).Should().BeFalse();
        File.Exists(schemaPath).Should().BeFalse("sidecars are cleaned up with the item that referenced them");
    }

    [Fact]
    public async Task PipelineIngestsAndQueriesAnAzureBlobPrefixEndToEnd()
    {
        var container = await azurite.SeedContainerAsync(new Dictionary<string, string>
        {
            ["docs/handbook.txt"] = "Refunds are available within thirty days with a receipt.",
            ["docs/records.csv"] = "id,content\n1,Shipping takes five days.\n2,Support replies within one day.\n",
            ["docs/records.csv.schema.json"] = Schema
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM_PROVIDER"] = "deterministic",
                ["DOC_STORE"] = "memory",
                ["VECTOR_STORE"] = "memory",
                ["AZURE_BLOB_CONNECTION_STRING"] = azurite.ConnectionString
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddRagPlatform(configuration)
            .AddRagAzureBlob(configuration)
            .BuildServiceProvider();

        var result = await provider.GetRequiredService<IIngestionPipeline>()
            .IngestAsync(new IngestionRequest($"azureblob://{container}/docs/"));

        result.DocumentIds.Should().HaveCount(3, "one text document plus one document per CSV row");
        result.ChunkIds.Should().NotBeEmpty();

        var answer = await provider.GetRequiredService<IQueryPipeline>()
            .QueryAsync(new QueryRequest("What is the refund policy?", Filter: new VectorSearchFilter(Origins: ["azureblob"])));

        answer.Citations.Should().NotBeEmpty();
        answer.Citations.Should().OnlyContain(citation => citation.Source.StartsWith($"azureblob://{container}/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingCredentialsAreReportedBeforeAnyNetworkCall()
    {
        var source = new AzureBlobDocumentSource(Options.Create(new AzureBlobOptions()), Options.Create(new CloudSourceOptions()));

        var act = () => ReadAllAsync(source, "azureblob://rag-docs/docs/");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AZURE_BLOB_CONNECTION_STRING*");
    }

    private AzureBlobDocumentSource Source()
    {
        return new AzureBlobDocumentSource(
            Options.Create(new AzureBlobOptions
            {
                ConnectionString = azurite.ConnectionString
            }),
            Options.Create(new CloudSourceOptions()));
    }

    private static async Task<IReadOnlyList<SourceItem>> ReadAllAsync(IDocumentSource source, string uri)
    {
        var items = new List<SourceItem>();
        await foreach (var item in source.EnumerateAsync(uri))
        {
            items.Add(item);
        }

        return items;
    }

    private static async Task DisposeAllAsync(IEnumerable<SourceItem> items)
    {
        foreach (var item in items)
        {
            await item.DisposeAsync();
        }
    }
}
