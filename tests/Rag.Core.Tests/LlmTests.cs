using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Llm;
using Xunit;

namespace Rag.Core.Tests;

public sealed class LlmTests
{
    [Fact]
    public async Task DeterministicEmbeddingsRespectConfiguredDimensions()
    {
        var client = new DeterministicLlmClient(Options.Create(new LlmOptions { EmbeddingDimensions = 7 }));

        var embedding = await client.EmbedAsync("refund policy");

        embedding.Should().HaveCount(7);
    }
}
