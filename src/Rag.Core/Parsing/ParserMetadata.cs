using Rag.Core.Models;

namespace Rag.Core.Parsing;

internal static class ParserMetadata
{
    public static DocumentMetadata ForFile(string path, string contentType)
    {
        var info = new FileInfo(path);
        var documentId = StableId(path, info.Length, info.LastWriteTimeUtc);
        return new DocumentMetadata(
            documentId,
            info.FullName,
            info.Name,
            Extension(path).TrimStart('.'),
            contentType,
            info.Length,
            DateTimeOffset.UtcNow);
    }

    private static string StableId(string path, long length, DateTime lastWriteTimeUtc)
    {
        var input = $"{Path.GetFullPath(path)}|{length}|{lastWriteTimeUtc:O}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)))[..24].ToLowerInvariant();
    }

    private static string Extension(string path)
    {
        if (path.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".jsonl.gz";
        }

        if (path.EndsWith(".ndjson.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".ndjson.gz";
        }

        return Path.GetExtension(path).ToLowerInvariant();
    }
}
