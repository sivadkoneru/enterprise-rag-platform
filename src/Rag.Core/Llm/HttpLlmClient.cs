using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Llm;

public sealed class HttpLlmClient(IHttpClientFactory httpClientFactory, IOptions<LlmOptions> options) : IEmbeddingClient, IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.EmbeddingEndpoint))
        {
            throw new InvalidOperationException("Llm:EmbeddingEndpoint is required for HTTP embeddings.");
        }

        using var request = BuildRequest(config.EmbeddingEndpoint, config.ApiKey);
        request.Content = JsonContent(new
        {
            model = config.EmbeddingModel,
            input
        });

        using var response = await SendWithRetryAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);
        var embedding = document.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return embedding.EnumerateArray().Select(value => value.GetSingle()).ToArray();
    }

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ChatEndpoint))
        {
            throw new InvalidOperationException("Llm:ChatEndpoint is required for HTTP chat completions.");
        }

        using var request = BuildRequest(config.ChatEndpoint, config.ApiKey);
        request.Content = JsonContent(new
        {
            model = config.ChatModel,
            messages = messages.Select(message => new { role = message.Role, content = message.Content })
        });

        using var response = await SendWithRetryAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("rag-llm");
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var clone = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
            var response = await client.SendAsync(clone, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode < 500 || attempt == 3)
            {
                response.EnsureSuccessStatusCode();
                return response;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Unreachable retry state.");
    }

    private static HttpRequestMessage BuildRequest(string endpoint, string? apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        }

        return request;
    }

    private static StringContent JsonContent<T>(T payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            clone.Content = new StringContent(body, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        return clone;
    }
}
