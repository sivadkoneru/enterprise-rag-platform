namespace Rag.Core.Models;

/// <summary>
/// Normalizes the <c>fileType</c> metadata value so ingestion, in-memory retrieval, and
/// backend retrieval filters all agree on a single lower-case, dot-prefixed representation.
/// </summary>
internal static class FileTypes
{
    public static string Normalize(string extension)
    {
        var lowered = extension.ToLowerInvariant();
        return lowered.StartsWith('.') ? lowered : $".{lowered}";
    }

    public static IReadOnlyList<string>? NormalizeAll(IReadOnlyList<string>? extensions)
    {
        return extensions?.Select(Normalize).ToArray();
    }
}
