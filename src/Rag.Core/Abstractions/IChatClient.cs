using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IChatClient
{
    Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
