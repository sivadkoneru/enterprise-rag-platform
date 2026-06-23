namespace Rag.Core.Common;

/// <summary>
/// Single source of truth for turning a path or file name into a normalized, lowercase file
/// extension. Compound suffixes (".jsonl.gz", ".ndjson.gz") are recognized as a whole extension
/// rather than truncated to ".gz" by <see cref="Path.GetExtension(string)"/>.
/// </summary>
internal static class FileExtensions
{
    public static string Normalize(string pathOrName)
    {
        if (pathOrName.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".jsonl.gz";
        }

        if (pathOrName.EndsWith(".ndjson.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".ndjson.gz";
        }

        return Path.GetExtension(pathOrName).ToLowerInvariant();
    }
}
