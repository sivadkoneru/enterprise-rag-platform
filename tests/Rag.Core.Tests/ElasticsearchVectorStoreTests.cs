using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Vector;
using Xunit;

namespace Rag.Core.Tests;

public sealed class ElasticsearchVectorStoreTests
{
    [Fact]
    public async Task UpsertIndexesEveryChunkInASingleBulkRequest()
    {
        var handler = new RecordingHandler(_ => Json("""{"errors":false,"items":[]}"""));
        var store = Store(handler);

        await store.UpsertAsync(
            [
                new VectorRecord("chunk-a", "doc", [1, 0], new Dictionary<string, string> { ["source"] = "a.txt" }),
                new VectorRecord("chunk-b", "doc", [0, 1], new Dictionary<string, string> { ["source"] = "b.txt" })
            ]);

        handler.Requests.Should().ContainSingle("chunks should be indexed with one _bulk round trip");
        var request = handler.Requests[0];
        request.Uri.Should().Contain("/_bulk");
        request.Uri.Should().Contain("refresh=true", "search must observe the chunks that were just written");
        request.Body.Should().Contain("\"_id\":\"chunk-a\"").And.Contain("\"_id\":\"chunk-b\"");
        request.Body.Should().EndWith("\n", "the bulk API requires a trailing newline");
    }

    [Fact]
    public async Task UpsertWithNoRecordsSkipsTheRoundTrip()
    {
        var handler = new RecordingHandler(_ => Json("""{"errors":false}"""));

        await Store(handler).UpsertAsync([]);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PartialBulkFailureIsReported()
    {
        var handler = new RecordingHandler(_ => Json("""
            {"errors":true,"items":[{"index":{"_id":"chunk-a","error":{"type":"mapper_parsing_exception","reason":"bad vector"}}}]}
            """));

        var act = () => Store(handler).UpsertAsync([new VectorRecord("chunk-a", "doc", [1, 0], new Dictionary<string, string>())]);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bad vector*");
    }

    [Fact]
    public async Task EnsureIndexTreatsAConcurrentCreateAsSuccess()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"type":"resource_already_exists_exception"}}""",
                    Encoding.UTF8,
                    "application/json")
            });

        var act = () => Store(handler).EnsureIndexAsync();

        await act.Should().NotThrowAsync("another worker creating the index first is not an error");
    }

    [Fact]
    public async Task EnsureIndexReportsRealCreationFailures()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":{"type":"illegal_argument_exception"}}""", Encoding.UTF8, "application/json")
            });

        var act = () => Store(handler).EnsureIndexAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*illegal_argument_exception*");
    }

    [Fact]
    public async Task ConfiguredCredentialsAreSentAsBasicAuthentication()
    {
        var handler = new RecordingHandler(_ => Json("""{"errors":false,"items":[]}"""));
        var store = new ElasticsearchVectorStore(
            new StubHttpClientFactory(handler),
            Options.Create(new VectorStoreOptions
            {
                Provider = "elasticsearch",
                Endpoint = "http://elastic.example:9200",
                IndexName = "rag-chunks",
                Username = "elastic",
                Password = "secret"
            }));

        await store.UpsertAsync([new VectorRecord("chunk-a", "doc", [1, 0], new Dictionary<string, string>())]);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("elastic:secret"));
        handler.Requests[0].Authorization.Should().Be($"Basic {expected}");
    }

    [Fact]
    public async Task FileTypeFiltersAreNormalizedToMatchIndexedMetadata()
    {
        var handler = new RecordingHandler(_ => Json("""{"hits":{"hits":[]}}"""));

        await Store(handler).SearchAsync([1, 0], 3, new VectorSearchFilter(FileTypes: ["PDF", ".TXT"]));

        handler.Requests[0].Body.Should().Contain(".pdf").And.Contain(".txt");
        handler.Requests[0].Body.Should().NotContain("PDF", "ingestion writes lower-case file types");
    }

    private static ElasticsearchVectorStore Store(RecordingHandler handler)
    {
        return new ElasticsearchVectorStore(
            new StubHttpClientFactory(handler),
            Options.Create(new VectorStoreOptions
            {
                Provider = "elasticsearch",
                Endpoint = "http://elastic.example:9200",
                IndexName = "rag-chunks",
                Dimensions = 2
            }));
    }

    private static HttpResponseMessage Json(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed record CapturedRequest(string Uri, string Body, string? Authorization);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(
                request.RequestUri!.ToString(),
                body,
                request.Headers.Authorization?.ToString()));
            return respond(request);
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
