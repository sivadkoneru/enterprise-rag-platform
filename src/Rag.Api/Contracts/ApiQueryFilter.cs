using Rag.Core.Models;

namespace Rag.Api.Contracts;

public sealed record ApiQueryFilter(
    IReadOnlyList<string>? Sources = null,
    IReadOnlyList<string>? DocumentIds = null,
    IReadOnlyList<string>? Documents = null,
    IReadOnlyList<string>? Origins = null,
    IReadOnlyList<string>? FileTypes = null,
    IReadOnlyList<string>? Types = null)
{
    public VectorSearchFilter? ToCoreFilter()
    {
        // Alias merging (Documents -> DocumentIds, Types -> FileTypes) is part of this request
        // contract, so it happens here, before the merged lists are handed to the shared
        // VectorSearchFilter.FromLists factory that both the API and the CLI use for normalization.
        return VectorSearchFilter.FromLists(
            documentIds: Merge(DocumentIds, Documents),
            sources: Sources,
            origins: Origins,
            fileTypes: Merge(FileTypes, Types));
    }

    private static IReadOnlyList<string>? Merge(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
    {
        var values = new List<string>();
        if (first is { Count: > 0 })
        {
            values.AddRange(first);
        }

        if (second is { Count: > 0 })
        {
            values.AddRange(second);
        }

        return values.Count == 0 ? null : values;
    }
}
