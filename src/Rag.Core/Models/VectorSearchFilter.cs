namespace Rag.Core.Models;

public sealed record VectorSearchFilter(
    IReadOnlyList<string>? DocumentIds = null,
    IReadOnlyList<string>? Sources = null,
    IReadOnlyList<string>? Origins = null,
    IReadOnlyList<string>? FileTypes = null)
{
    /// <summary>
    /// Single source of truth for turning caller-supplied filter lists into a <see cref="VectorSearchFilter"/>.
    /// Both the API and the CLI call this so their normalization rules cannot drift apart: every field is
    /// normalized independently (an empty or null list becomes <c>null</c>), and the whole filter collapses
    /// to <c>null</c> when nothing was supplied, so callers can pass the result straight through as "no filter".
    /// </summary>
    public static VectorSearchFilter? FromLists(
        IReadOnlyList<string>? documentIds,
        IReadOnlyList<string>? sources,
        IReadOnlyList<string>? origins,
        IReadOnlyList<string>? fileTypes)
    {
        var normalizedDocumentIds = Normalize(documentIds);
        var normalizedSources = Normalize(sources);
        var normalizedOrigins = Normalize(origins);
        var normalizedFileTypes = Normalize(fileTypes);

        if (normalizedDocumentIds is null && normalizedSources is null && normalizedOrigins is null && normalizedFileTypes is null)
        {
            return null;
        }

        return new VectorSearchFilter(normalizedDocumentIds, normalizedSources, normalizedOrigins, normalizedFileTypes);
    }

    private static IReadOnlyList<string>? Normalize(IReadOnlyList<string>? values)
    {
        return values is { Count: > 0 } ? values : null;
    }
}
