using System.Collections.Concurrent;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Vector;

public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, VectorRecord> _records = new();

    public Task EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _records[record.ChunkId] = record;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        IReadOnlyList<float> queryVector,
        int topK,
        VectorSearchFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var results = _records.Values
            .Where(record => Matches(record, filter))
            .Select(record =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new VectorSearchResult(record.ChunkId, record.DocumentId, Cosine(queryVector, record.Vector));
            })
            .OrderByDescending(result => result.Score)
            .Take(Math.Max(1, topK))
            .ToArray();
        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    private static bool Matches(VectorRecord record, VectorSearchFilter? filter)
    {
        if (filter is null)
        {
            return true;
        }

        return MatchesAny(record.DocumentId, filter.DocumentIds) &&
            MatchesMetadata(record, "source", filter.Sources) &&
            MatchesMetadata(record, "origin", filter.Origins) &&
            MatchesMetadata(record, "fileType", NormalizeFileTypes(filter.FileTypes));
    }

    private static bool MatchesMetadata(VectorRecord record, string key, IReadOnlyList<string>? values)
    {
        return values is not { Count: > 0 } ||
            record.Metadata.TryGetValue(key, out var value) &&
            MatchesAny(value, values);
    }

    private static bool MatchesAny(string value, IReadOnlyList<string>? candidates)
    {
        return candidates is not { Count: > 0 } ||
            candidates.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string>? NormalizeFileTypes(IReadOnlyList<string>? fileTypes)
    {
        return fileTypes?.Select(type => type.StartsWith('.') ? type : $".{type}").ToArray();
    }

    private static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var length = Math.Min(left.Count, right.Count);
        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
