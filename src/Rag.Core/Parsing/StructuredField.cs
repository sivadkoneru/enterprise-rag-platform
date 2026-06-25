namespace Rag.Core.Parsing;

internal sealed record StructuredField(
    string? Path,
    string? Column,
    string Format = "plain",
    string? Label = null,
    bool Required = false);
