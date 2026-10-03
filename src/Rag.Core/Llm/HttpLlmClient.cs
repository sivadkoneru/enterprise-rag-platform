using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Common;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Llm;

public sealed class HttpLlmClient(
    IHttpClientFactory httpClientFactory,
    IOptions<LlmOptions> options,
    ILogger<HttpLlmClient> logger) : IEmbeddingUsageClient, IChatUsageClient
{
    public async Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        return (await EmbedDetailedAsync(input, cancellationToken).ConfigureAwait(false)).Vector;
    }

    public async Task<EmbeddingResult> EmbedDetailedAsync(string input, CancellationToken cancellationToken = default)
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

        logger.LogDebug("Calling embedding provider.");
        var payload = await SendAsync(request, "embedding", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() == 0 ||
            !data[0].TryGetProperty("embedding", out var embedding))
        {
            throw new InvalidOperationException("The embedding endpoint returned a response without a 'data[0].embedding' array.");
        }

        int? tokens = document.RootElement.TryGetProperty("usage", out var usage) && usage.TryGetProperty("total_tokens", out var total) && total.TryGetInt32(out var count) && count >= 0 ? count : null;
        if (tokens is not null) { RagTelemetry.Tokens.Add(tokens.Value, new KeyValuePair<string, object?>("operation", "embedding")); }
        return new EmbeddingResult(embedding.EnumerateArray().Select(value => value.GetSingle()).ToArray(), tokens);
    }

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        return (await CompleteDetailedAsync(messages, cancellationToken).ConfigureAwait(false)).Text;
    }

    public async Task<ChatCompletionResult> CompleteDetailedAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
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
            max_tokens = config.MaxOutputTokens,
            messages = messages.Select(message => new { role = message.Role, content = message.Content })
        });

        logger.LogDebug("Calling chat provider.");
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

        int? Usage(string key)
        {
            return document.RootElement.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty(key, out var number) && number.TryGetInt32(out var count) && count >= 0 ? count : null;
        }
        return new ChatCompletionResult(content.GetString() ?? string.Empty, Usage("prompt_tokens"), Usage("completion_tokens"), Usage("total_tokens"));
    }

    // Timeouts, retries, and backoff come from the "rag-llm" resilience handler configured in
    // AddRagPlatform, so this sends the request exactly once and lets the pipeline retry it.
    private async Task<string> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        using var activity = RagTelemetry.Activities.StartActivity($"model.{operation}");
        var client = httpClientFactory.CreateClient("rag-llm");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            RagTelemetry.ProviderFailures.Add(1, new KeyValuePair<string, object?>("operation", operation));
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            logger.LogWarning("LLM {Operation} failed with HTTP {StatusCode}.", operation, (int)response.StatusCode);
            throw new HttpRequestException($"LLM {operation} failed with HTTP {(int)response.StatusCode}.", null, response.StatusCode);
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
        return new StringContent(JsonSerializer.Serialize(payload, RagJson.Options), Encoding.UTF8, "application/json");
    }

}
