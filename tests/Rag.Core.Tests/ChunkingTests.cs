using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Chunking;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

public sealed class ChunkingTests
{
    [Fact]
    public async Task FixedStrategyProducesOverlappingChunks()
    {
        var strategy = new FixedChunkingStrategy(Options.Create(new ChunkingOptions { Size = 10, Overlap = 2 }));
        var document = Document("abcdefghijklmnopqrstuvwxyz");

        var chunks = await strategy.ChunkAsync(document);

        chunks.Should().HaveCount(3);
        chunks[1].StartOffset.Should().Be(8);
    }

    [Fact]
    public async Task RecursiveStrategyKeepsChunksUnderConfiguredSize()
    {
        var strategy = new RecursiveChunkingStrategy(Options.Create(new ChunkingOptions { Size = 20 }));
        var document = Document("First sentence. Second sentence. Third sentence.");

        var chunks = await strategy.ChunkAsync(document);

        chunks.Should().OnlyContain(chunk => chunk.Text.Length <= 20);
    }

    private static ParsedDocument Document(string text)
    {
        var metadata = new DocumentMetadata("doc", "memory", "memory.txt", "txt", "text/plain", text.Length, DateTimeOffset.UtcNow);
        return new ParsedDocument("doc", text, metadata);
    }
}
