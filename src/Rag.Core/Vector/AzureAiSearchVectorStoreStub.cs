using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Vector;

public sealed class AzureAiSearchVectorStoreStub : IAzureAiSearchVectorStore
{
    private const string NotImplementedMessage = "Azure AI Search is intentionally stubbed for a future phase.";

    public Task EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        IReadOnlyList<float> queryVector,
        int topK,
        VectorSearchFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }
}
