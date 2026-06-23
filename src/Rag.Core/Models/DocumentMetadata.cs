namespace Rag.Core.Models;

public sealed record DocumentMetadata(
    string DocumentId,
    string Source,
    string FileName,
    string Extension,
    string ContentType,
    long Length,
    DateTimeOffset IngestedAt,
    IReadOnlyDictionary<string, string>? Attributes = null,
    string Origin = "file");
