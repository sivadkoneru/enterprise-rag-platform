using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Llm;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

public sealed class PromptGroundingTests
{
    [Fact]
    public void LlmOptionsBindConfigurableSystemPromptFromEnvironmentStyleConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM_SYSTEM_PROMPT"] = "Use only the supplied test context."
            })
            .Build();

        using var services = new ServiceCollection()
            .AddRagPlatform(configuration)
            .BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<LlmOptions>>().Value;
        var property = typeof(LlmOptions).GetProperty("SystemPrompt");

        property.Should().NotBeNull("LLM_SYSTEM_PROMPT should bind to LlmOptions.SystemPrompt");
        property!.GetValue(options).Should().Be("Use only the supplied test context.");
    }

    [Fact]
    public async Task QueryPipelineSendsConfiguredGroundingSystemPromptOnEveryChatCall()
    {
        var chat = new RecordingChatClient();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM_SYSTEM_PROMPT"] = "Ground every answer in the provided test context."
            })
            .Build();

        using var services = new ServiceCollection()
            .AddRagPlatform(configuration)
            .AddSingleton<IEmbeddingClient>(new ConstantEmbeddingClient())
            .AddSingleton<IChatClient>(chat)
            .BuildServiceProvider();

        await services.GetRequiredService<IQueryPipeline>().QueryAsync(new QueryRequest("What can I answer?"));

        chat.Messages.Should().NotBeEmpty();
        chat.Messages[0].Role.Should().Be("system");
        chat.Messages[0].Content.Should().Be("Ground every answer in the provided test context.");
    }

    [Fact]
    public async Task DeterministicClientReturnsNoAnswerWhenGroundedContextIsEmpty()
    {
        var client = new DeterministicLlmClient(Options.Create(new LlmOptions()));

        var answer = await client.CompleteAsync(
            [
                new ChatMessage("system", "Answer only from supplied context. If absent, say you don't know."),
                new ChatMessage("user", "Question: What is the refund policy?\nContext:\n")
            ]);

        answer.Should().Contain("don't know", "the deterministic provider should honor the grounding contract in local defaults");
    }

    private sealed class ConstantEmbeddingClient : IEmbeddingClient
    {
        public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float>>([1, 0, 0]);
        }
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            Messages = messages;
            return Task.FromResult("recorded");
        }
    }
}
