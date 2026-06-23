namespace Rag.Core.Models;

public sealed record ParsedDocument(
    string Id,
    string Text,
    DocumentMetadata Metadata);
