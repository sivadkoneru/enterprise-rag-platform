using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IEmbeddingClient
{
    Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default);
}

public interface IChatClient
{
    Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
