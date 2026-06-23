namespace Rag.Core.Models;

public sealed record TextChunk(
    string Id,
    string DocumentId,
    int Index,
    string Text,
    int StartOffset,
    int EndOffset,
    DocumentMetadata Metadata);
