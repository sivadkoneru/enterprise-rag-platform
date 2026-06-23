using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task IngestThenQueryReturnsCitation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "Refunds are available within thirty days with a receipt.");
        var services = new ServiceCollection()
            .AddRagPlatform(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        await services.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest(path));
        var answer = await services.GetRequiredService<IQueryPipeline>().QueryAsync(new QueryRequest("What is the refund policy?"));

        answer.Citations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IngestStructuredFileIndexesEveryRecordAsDocument()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rag-structured-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "policies.csv");
        await File.WriteAllTextAsync(path, "id,title,content\none,Refunds,Refunds are available within thirty days.\ntwo,Shipping,Shipping takes five days.\n");
        await File.WriteAllTextAsync($"{path}.schema.json", """
            {
              "version": 1,
              "profiles": [
                {
                  "files": ["*.csv"],
                  "format": "csv",
                  "id": "id",
                  "text": [
                    { "column": "title", "label": "Title" },
                    { "column": "content", "required": true }
                  ]
                }
              ]
            }
            """);

        try
        {
            var services = new ServiceCollection()
                .AddRagPlatform(new ConfigurationBuilder().Build())
                .BuildServiceProvider();

            var result = await services.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest(directory));

            result.DocumentIds.Should().HaveCount(2);
            result.ChunkIds.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task IngestDirectoryWithMultipleJsonFilesIndexesEveryJsonObject()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rag-json-corpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "first.json"), """{ "id": "first", "title": "First", "body": "First JSON document." }""");
        await File.WriteAllTextAsync(Path.Combine(directory, "second.json"), """{ "id": "second", "title": "Second", "body": "Second JSON document." }""");
        await File.WriteAllTextAsync(Path.Combine(directory, "rag-ingestion.schema.json"), """
            {
              "version": 1,
              "profiles": [
                {
                  "files": ["*.json"],
                  "format": "json",
                  "id": "/id",
                  "text": [
                    { "path": "/title", "label": "Title" },
                    { "path": "/body", "required": true }
                  ]
                }
              ]
            }
            """);

        try
        {
            var services = new ServiceCollection()
                .AddRagPlatform(new ConfigurationBuilder().Build())
                .BuildServiceProvider();

            var result = await services.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest(directory));

            result.DocumentIds.Should().HaveCount(2);
            result.ChunkIds.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
