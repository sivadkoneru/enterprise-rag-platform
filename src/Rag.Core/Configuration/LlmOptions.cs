namespace Rag.Core.Configuration;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "deterministic";

    public string? ApiKey { get; set; }

    public string? EmbeddingEndpoint { get; set; }

    public string? EmbeddingModel { get; set; }

    public int EmbeddingDimensions { get; set; } = 1536;

    public string? ChatEndpoint { get; set; }

    public string? ChatModel { get; set; }

    public string SystemPrompt { get; set; } = "Answer only from the supplied context. If the answer is not present, say you don't know. Always cite sources when context is used.";

    public int MaxOutputTokens { get; set; } = 1024;

    public int TimeoutSeconds { get; set; } = 60;

    public int RetryCount { get; set; } = 3;

    public int RetryBackoffSeconds { get; set; } = 2;
}
