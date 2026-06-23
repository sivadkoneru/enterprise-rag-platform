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

        var payload = await SendAsync(request, "embedding", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() == 0 ||
            !data[0].TryGetProperty("embedding", out var embedding))
        {
            throw new InvalidOperationException("The embedding endpoint returned a response without a 'data[0].embedding' array.");
        }

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

        var payload = await SendAsync(request, "chat", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            throw new InvalidOperationException("The chat endpoint returned a response without a 'choices[0].message.content' value.");
        }

        return content.GetString() ?? string.Empty;
    }

    // Timeouts, retries, and backoff come from the "rag-llm" resilience handler configured in
    // AddRagPlatform, so this sends the request exactly once and lets the pipeline retry it.
    private async Task<string> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("rag-llm");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"The LLM {operation} endpoint returned {(int)response.StatusCode} ({response.ReasonPhrase}): {Truncate(payload)}",
                inner: null,
                response.StatusCode);
        }

        return payload;
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

    private static string Truncate(string payload)
    {
        const int limit = 500;
        return payload.Length <= limit ? payload : $"{payload[..limit]}...";
    }
}
