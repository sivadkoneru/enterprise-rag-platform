namespace Rag.Core.Models;

public sealed record QueryRequest(string Question, int TopK = 5, VectorSearchFilter? Filter = null);
