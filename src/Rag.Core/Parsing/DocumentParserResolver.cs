using Rag.Core.Abstractions;

namespace Rag.Core.Parsing;

public sealed class DocumentParserResolver(IEnumerable<IDocumentParser> parsers) : IDocumentParserResolver
{
    private readonly IReadOnlyList<IDocumentParser> _parsers = parsers.ToArray();

    public IReadOnlyList<IDocumentParser> Parsers => _parsers;

    public IDocumentParser Resolve(string path, string? contentType = null)
    {
        var parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(path, contentType));
        return parser ?? throw new NotSupportedException($"No document parser is registered for '{path}'.");
    }
}
