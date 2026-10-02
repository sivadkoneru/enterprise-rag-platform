using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Rag.Core.Workbench;

public sealed class HttpRerankerClient(IHttpClientFactory clients, IOptions<RerankerOptions> options) : IRerankerClient
{
    public async Task<IReadOnlyDictionary<int, double>> RerankAsync(string question, IReadOnlyList<string> documents, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Configure an HTTP reranker endpoint before enabling reranking.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new { model = config.Model, query = question, documents, top_n = documents.Count })
        };
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 1, 600)));
        using var response = await clients.CreateClient("rag-reranker").SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false), cancellationToken: timeout.Token).ConfigureAwait(false);
        var result = new Dictionary<int, double>();
        foreach (var item in payload.RootElement.GetProperty("results").EnumerateArray())
        {
            var index = item.GetProperty("index").GetInt32();
            var score = item.GetProperty("relevance_score").GetDouble();
            if (index < 0 || index >= documents.Count || !double.IsFinite(score) || !result.TryAdd(index, score))
            {
                throw new InvalidDataException("Reranker returned invalid result indices or scores.");
            }
        }
        if (result.Count != documents.Count)
        {
            throw new InvalidDataException("Reranker did not return a score for every candidate.");
        }

        return result;
    }
}
