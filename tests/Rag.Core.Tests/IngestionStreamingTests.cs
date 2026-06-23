using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

public sealed class IngestionStreamingTests
{
    [Fact]
    public async Task SourceItemsAreProcessedAndReleasedAsTheyArriveRatherThanBufferedUpFront()
    {
        // Cloud adapters materialize a temporary file per item as it is yielded. Streaming keeps
        // at most MaxDegreeOfParallelism of them on disk instead of the whole prefix.
        var source = new TrackingSource(itemCount: 3);
        using var services = new ServiceCollection()
            .AddRagPlatform(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["INGESTION_MAX_PARALLELISM"] = "1" })
                .Build())
            .AddSingleton<IDocumentSource>(source)
            .BuildServiceProvider();

        var result = await services.GetRequiredService<IIngestionPipeline>()
            .IngestAsync(new IngestionRequest("teststream://corpus"));

        result.ChunkIds.Should().HaveCount(3);
        source.Events.Should().Equal(
            "yield:0", "dispose:0",
            "yield:1", "dispose:1",
            "yield:2", "dispose:2");
        source.TempPaths.Should().OnlyContain(path => !File.Exists(path), "each item is cleaned up once it is ingested");
    }

    [Fact]
    public async Task FailureStillReleasesTheItemBeingProcessed()
    {
        var source = new TrackingSource(itemCount: 2, corruptAt: 1);
        using var services = new ServiceCollection()
            .AddRagPlatform(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["INGESTION_MAX_PARALLELISM"] = "1" })
                .Build())
            .AddSingleton<IDocumentSource>(source)
            .BuildServiceProvider();

        var act = () => services.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest("teststream://corpus"));

        await act.Should().ThrowAsync<Exception>();
        source.Events.Should().Contain("dispose:1", "a failing item must still be cleaned up");
        source.TempPaths.Should().OnlyContain(path => !File.Exists(path));
    }

    private sealed class TrackingSource(int itemCount, int? corruptAt = null) : IDocumentSource
    {
        private readonly Lock _gate = new();

        public string Scheme => "teststream";

        public List<string> Events { get; } = [];

        public List<string> TempPaths { get; } = [];

        public bool CanRead(string sourceUri)
        {
            return sourceUri.StartsWith("teststream://", StringComparison.OrdinalIgnoreCase);
        }

        public async IAsyncEnumerable<SourceItem> EnumerateAsync(
            string sourceUri,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < itemCount; index++)
            {
                var directory = Path.Combine(Path.GetTempPath(), $"rag-stream-{Guid.NewGuid():N}");
                Directory.CreateDirectory(directory);

                // An unsupported extension makes the parser resolver throw for the corrupt item.
                var path = Path.Combine(directory, index == corruptAt ? "item.unsupported" : "item.txt");
                await File.WriteAllTextAsync(path, $"Document {index} content.", cancellationToken);

                var current = index;
                Record($"yield:{current}");
                lock (_gate)
                {
                    TempPaths.Add(path);
                }

                yield return new SourceItem(
                    path,
                    $"teststream://corpus/item{current}",
                    Scheme,
                    Path.GetFileName(path),
                    Path.GetExtension(path),
                    cleanup: () =>
                    {
                        Record($"dispose:{current}");
                        Directory.Delete(directory, recursive: true);
                        return ValueTask.CompletedTask;
                    });
            }
        }

        private void Record(string entry)
        {
            lock (_gate)
            {
                Events.Add(entry);
            }
        }
    }
}
