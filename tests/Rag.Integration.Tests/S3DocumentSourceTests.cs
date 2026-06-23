using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using Rag.Core.Sources;
using Rag.Providers.Aws;
using Xunit;

namespace Rag.Integration.Tests;

[Collection(CloudSourceCollection.Name)]
[Trait("Category", "Integration")]
public sealed class S3DocumentSourceTests(LocalStackFixture localStack) : IClassFixture<LocalStackFixture>
{
    private const string Schema = """
        {
          "version": 1,
          "profiles": [
            {
              "files": ["*.jsonl"],
              "format": "jsonl",
              "id": "/id",
              "text": [{ "path": "/body", "required": true }]
            }
          ]
        }
        """;

    [Fact]
    public async Task SupportedObjectsAreMaterializedInStableOrderAndUnsupportedOnesSkipped()
    {
        var bucket = await localStack.SeedBucketAsync(new Dictionary<string, string>
        {
            ["docs/handbook.txt"] = "Refunds are available within thirty days.",
            ["docs/policies.md"] = "# Policies\n\nShipping takes five days.",
            ["docs/notes.tmp"] = "unsupported",
            ["other/excluded.txt"] = "outside the prefix"
        });

        using var client = localStack.CreateClient();
        var items = await ReadAllAsync(new AwsS3DocumentSource(client, Options.Create(new CloudSourceOptions())), $"s3://{bucket}/docs/");

        try
        {
            items.Select(item => item.FileName).Should().Equal("handbook.txt", "policies.md");
            items.Select(item => item.Origin).Should().OnlyContain(origin => origin == "s3");
            items.Select(item => item.Source).Should().Equal(
                $"s3://{bucket}/docs/handbook.txt",
                $"s3://{bucket}/docs/policies.md");
            items.Select(item => item.Extension).Should().Equal(".txt", ".md");

            var downloaded = await File.ReadAllTextAsync(items[0].LocalPath);
            downloaded.Should().Be("Refunds are available within thirty days.");
            items[0].Attributes.Should().ContainKey("bucket").WhoseValue.Should().Be(bucket);
            items[0].Attributes.Should().ContainKey("key").WhoseValue.Should().Be("docs/handbook.txt");
        }
        finally
        {
            await DisposeAllAsync(items);
        }
    }

    [Fact]
    public async Task SchemaSidecarsAreDownloadedAsAttributesAndNeverIngestedAsDocuments()
    {
        var bucket = await localStack.SeedBucketAsync(new Dictionary<string, string>
        {
            ["docs/records.jsonl"] = """{"id":"a","body":"Alpha record."}""",
            ["docs/records.jsonl.schema.json"] = Schema
        });

        using var client = localStack.CreateClient();
        var items = await ReadAllAsync(new AwsS3DocumentSource(client, Options.Create(new CloudSourceOptions())), $"s3://{bucket}/docs/");

        try
        {
            items.Should().ContainSingle("the schema sidecar is ingestion metadata, not a document");
            items[0].FileName.Should().Be("records.jsonl");

            items[0].Attributes.Should().ContainKey("schemaPath");
            var schemaPath = items[0].Attributes!["schemaPath"];
            File.Exists(schemaPath).Should().BeTrue("the sidecar must be materialized next to the record file");
            (await File.ReadAllTextAsync(schemaPath)).Should().Contain("\"format\": \"jsonl\"");
        }
        finally
        {
            await DisposeAllAsync(items);
        }
    }

    [Fact]
    public async Task DisposingASourceItemRemovesItsTemporaryFiles()
    {
        var bucket = await localStack.SeedBucketAsync(new Dictionary<string, string>
        {
            ["docs/records.jsonl"] = """{"id":"a","body":"Alpha record."}""",
            ["docs/records.jsonl.schema.json"] = Schema
        });

        using var client = localStack.CreateClient();
        var items = await ReadAllAsync(new AwsS3DocumentSource(client, Options.Create(new CloudSourceOptions())), $"s3://{bucket}/docs/");
        var localPath = items[0].LocalPath;
        var schemaPath = items[0].Attributes!["schemaPath"];

        await DisposeAllAsync(items);

        File.Exists(localPath).Should().BeFalse();
        File.Exists(schemaPath).Should().BeFalse("sidecars are cleaned up with the item that referenced them");
    }

    [Fact]
    public async Task PipelineIngestsAndQueriesAnS3PrefixEndToEnd()
    {
        var bucket = await localStack.SeedBucketAsync(new Dictionary<string, string>
        {
            ["docs/handbook.txt"] = "Refunds are available within thirty days with a receipt.",
            ["docs/records.jsonl"] = """
                {"id":"a","body":"Shipping takes five days."}
                {"id":"b","body":"Support replies within one day."}
                """,
            ["docs/records.jsonl.schema.json"] = Schema
        });

        var s3Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM_PROVIDER"] = "deterministic",
                ["DOC_STORE"] = "memory",
                ["VECTOR_STORE"] = "memory",
                ["S3_ENDPOINT"] = localStack.ServiceUrl
            })
            .Build();
        var services = new ServiceCollection()
            .AddRagPlatform(s3Configuration)
            .AddRagAwsS3(s3Configuration);

        // Override the SDK client so the run uses explicit LocalStack credentials.
        services.AddSingleton(localStack.CreateClient());
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IIngestionPipeline>()
            .IngestAsync(new IngestionRequest($"s3://{bucket}/docs/"));

        result.DocumentIds.Should().HaveCount(3, "one text document plus one document per JSONL record");
        result.ChunkIds.Should().NotBeEmpty();

        var answer = await provider.GetRequiredService<IQueryPipeline>()
            .QueryAsync(new QueryRequest("What is the refund policy?", Filter: new VectorSearchFilter(Origins: ["s3"])));

        answer.Citations.Should().NotBeEmpty();
        answer.Citations.Should().OnlyContain(citation => citation.Source.StartsWith($"s3://{bucket}/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TemporaryFilesDoNotOutliveAnIngestionRun()
    {
        var bucket = await localStack.SeedBucketAsync(new Dictionary<string, string>
        {
            ["docs/handbook.txt"] = "Refunds are available within thirty days."
        });

        var temporaryFilesConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM_PROVIDER"] = "deterministic",
                ["DOC_STORE"] = "memory",
                ["VECTOR_STORE"] = "memory"
            })
            .Build();
        var services = new ServiceCollection()
            .AddRagPlatform(temporaryFilesConfiguration)
            .AddRagAwsS3(temporaryFilesConfiguration);
        services.AddSingleton(localStack.CreateClient());
        using var provider = services.BuildServiceProvider();

        var before = TempIngestDirectoryCount();
        await provider.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest($"s3://{bucket}/docs/"));

        TempIngestDirectoryCount().Should().Be(before, "streamed source items are released as they are ingested");
    }

    private static int TempIngestDirectoryCount()
    {
        var root = Path.Combine(Path.GetTempPath(), "rag-ingest");
        return Directory.Exists(root) ? Directory.GetDirectories(root).Length : 0;
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
