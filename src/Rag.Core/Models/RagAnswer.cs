namespace Rag.Core.Models;

public sealed record RagAnswer(
    string Answer,
    IReadOnlyList<SourceCitation> Citations);
