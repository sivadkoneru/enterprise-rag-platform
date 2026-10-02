using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public sealed record ChatCompletionResult(string Text, int? PromptTokens = null, int? CompletionTokens = null, int? TotalTokens = null);

/// <summary>Optional provider-reported usage, without changing the original chat contract.</summary>
public interface IChatUsageClient : IChatClient
{
    Task<ChatCompletionResult> CompleteDetailedAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
