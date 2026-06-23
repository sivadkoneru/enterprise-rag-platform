using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IQueryPipeline
{
    Task<RagAnswer> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default);
}
