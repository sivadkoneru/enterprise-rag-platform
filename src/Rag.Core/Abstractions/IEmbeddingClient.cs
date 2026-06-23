namespace Rag.Core.Abstractions;

public interface IEmbeddingClient
{
    Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default);
}
