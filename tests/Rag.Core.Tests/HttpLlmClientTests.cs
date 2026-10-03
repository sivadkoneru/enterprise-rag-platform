using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Llm;
using Rag.Core.Models;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Rag.Core.Tests;

public sealed class HttpLlmClientTests
{
    [Fact]
    public async Task EmbedSendsExactlyOneRequestSoRetriesStayWithTheResiliencePipeline()
    {
        var handler = new RecordingHandler(_ => Json("""{"data":[{"embedding":[0.5,0.25]}]}"""));
        var client = new HttpLlmClient(new StubHttpClientFactory(handler), Options.Create(EndpointOptions()), NullLogger<HttpLlmClient>.Instance);

        var embedding = await client.EmbedAsync("refund policy");

        embedding.Should().Equal(0.5f, 0.25f);
        handler.Requests.Should().Be(1, "the named client already applies the configured retry policy");
    }

    [Fact]
    public async Task CompleteReturnsTheFirstChoiceContent()
    {
        var handler = new RecordingHandler(_ => Json("""{"choices":[{"message":{"content":"grounded answer"}}]}"""));
        var client = new HttpLlmClient(new StubHttpClientFactory(handler), Options.Create(EndpointOptions()), NullLogger<HttpLlmClient>.Instance);

        var answer = await client.CompleteAsync([new ChatMessage("user", "question")]);

        answer.Should().Be("grounded answer");
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task ServerErrorSurfacesStatusCodeWithoutLeakingBody()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("upstream exploded", Encoding.UTF8, "text/plain")
        });
        var client = new HttpLlmClient(new StubHttpClientFactory(handler), Options.Create(EndpointOptions()), NullLogger<HttpLlmClient>.Instance);

        var act = () => client.EmbedAsync("refund policy");

        var failure = await act.Should().ThrowAsync<HttpRequestException>();
        failure.Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        failure.Which.Message.Should().NotContain("upstream exploded");
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task UnexpectedEmbeddingShapeFailsWithAnActionableMessage()
    {
        var handler = new RecordingHandler(_ => Json("""{"data":[]}"""));
        var client = new HttpLlmClient(new StubHttpClientFactory(handler), Options.Create(EndpointOptions()), NullLogger<HttpLlmClient>.Instance);

        var act = () => client.EmbedAsync("refund policy");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*data[0].embedding*");
    }

    [Fact]
    public async Task MissingEndpointIsReportedBeforeAnyRequestIsSent()
    {
        var handler = new RecordingHandler(_ => Json("{}"));
        var client = new HttpLlmClient(new StubHttpClientFactory(handler), Options.Create(new LlmOptions { Provider = "openai" }), NullLogger<HttpLlmClient>.Instance);

        var act = () => client.EmbedAsync("refund policy");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EmbeddingEndpoint*");
        handler.Requests.Should().Be(0);
    }

    private static LlmOptions EndpointOptions()
    {
        return new LlmOptions
        {
            Provider = "openai",
            ApiKey = "test-key",
            EmbeddingEndpoint = "https://llm.example/v1/embeddings",
            ChatEndpoint = "https://llm.example/v1/chat/completions"
        };
    }

    private static HttpResponseMessage Json(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private int _requests;

        public int Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }
}
