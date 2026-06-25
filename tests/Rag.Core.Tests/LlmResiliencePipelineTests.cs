using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Core.Llm;
using Xunit;

namespace Rag.Core.Tests;

public sealed class LlmResiliencePipelineTests
{
    [Fact]
    public async Task TransientFailuresAreRetriedByTheConfiguredResilienceHandler()
    {
        // HttpLlmClient sends each request once and relies on the "rag-llm" pipeline to retry it.
        // That only works if the pipeline can re-send the same request message, so assert it here
        // rather than assuming it.
        var handler = new FailThenSucceedHandler(failures: 1);
        var services = new ServiceCollection()
            .AddRagPlatform(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LLM_PROVIDER"] = "openai",
                    ["LLM_EMBEDDING_ENDPOINT"] = "https://llm.example/v1/embeddings",
                    ["LLM_RETRY_COUNT"] = "2",
                    ["LLM_RETRY_BACKOFF_SECONDS"] = "1"
                })
                .Build());
        services.AddHttpClient("rag-llm").ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();

        var embedding = await provider.GetRequiredService<IEmbeddingClient>().EmbedAsync("refund policy");

        embedding.Should().Equal(0.5f, 0.25f);
        handler.Attempts.Should().Be(2, "the transient failure should be retried once and then succeed");
        provider.GetRequiredService<IEmbeddingClient>().Should().BeOfType<HttpLlmClient>();
    }

    private sealed class FailThenSucceedHandler(int failures) : HttpMessageHandler
    {
        private int _attempts;

        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            if (attempt <= failures)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"embedding":[0.5,0.25]}]}""", Encoding.UTF8, "application/json")
            });
        }
    }
}
