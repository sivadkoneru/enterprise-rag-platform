namespace Rag.Api.Contracts;

public sealed record ApiQueryRequest(string Question, int TopK = 5, ApiQueryFilter? Filter = null);
