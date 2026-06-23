namespace Rag.Core.Abstractions;

public interface IDocumentParserResolver
{
    IDocumentParser Resolve(string path, string? contentType = null);

    IReadOnlyList<IDocumentParser> Parsers { get; }
}
