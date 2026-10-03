namespace Rag.Core.Abstractions;

public sealed record EmbeddingResult(IReadOnlyList<float> Vector, int? InputTokens = null);

/// <summary>Optional provider usage without changing existing embedding adapters.</summary>
public interface IEmbeddingUsageClient : IEmbeddingClient
{
    Task<EmbeddingResult> EmbedDetailedAsync(string input, CancellationToken cancellationToken = default);
}
