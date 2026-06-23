using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IVectorStore
{
    Task EnsureIndexAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(IReadOnlyList<float> queryVector, int topK, CancellationToken cancellationToken = default);
}
