using Rag.Core.Models;

namespace Rag.Core.Workbench;

public interface ILexicalSearchStore
{
    Task<IReadOnlyList<VectorSearchResult>> SearchLexicalAsync(string question, int topK, VectorSearchFilter filter, CancellationToken cancellationToken = default);
}
