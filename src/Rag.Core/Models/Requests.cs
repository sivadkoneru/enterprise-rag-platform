namespace Rag.Core.Models;

public sealed record IngestionRequest(string? Path = null, string? Strategy = null, IReadOnlyList<string>? Sources = null)
{
    public IReadOnlyList<string> SourceUris
    {
        get
        {
            if (Sources is { Count: > 0 })
            {
                return Sources;
            }

            return string.IsNullOrWhiteSpace(Path) ? [] : [Path];
        }
    }
}

public sealed record IngestionResult(
    string DocumentId,
    int ChunkCount,
    string Strategy,
    IReadOnlyList<string> ChunkIds,
    IReadOnlyList<string>? DocumentIds = null);

public sealed record QueryRequest(string Question, int TopK = 5, VectorSearchFilter? Filter = null);

public sealed record VectorSearchFilter(
    IReadOnlyList<string>? DocumentIds = null,
    IReadOnlyList<string>? Sources = null,
    IReadOnlyList<string>? Origins = null,
    IReadOnlyList<string>? FileTypes = null);

public sealed record ChatMessage(string Role, string Content);
