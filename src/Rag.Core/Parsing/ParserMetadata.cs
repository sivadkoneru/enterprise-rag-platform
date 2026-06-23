using Rag.Core.Common;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

internal static class ParserMetadata
{
    public static DocumentMetadata ForFile(string path, string contentType)
    {
        var info = new FileInfo(path);
        var documentId = StableId.Compute($"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc:O}");
        return new DocumentMetadata(
            documentId,
            info.FullName,
            info.Name,
            Extension(path).TrimStart('.'),
            contentType,
            info.Length,
            DateTimeOffset.UtcNow);
    }

    private static string Extension(string path)
    {
        return FileExtensions.Normalize(path);
    }
}
