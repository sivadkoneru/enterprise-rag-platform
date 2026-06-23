using System.Runtime.CompilerServices;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

public sealed class DocumentParserResolver(
    IEnumerable<IDocumentParser> parsers,
    IEnumerable<IMultiDocumentParser> multiDocumentParsers) : IDocumentParserResolver
{
    private readonly IReadOnlyList<IDocumentParser> _parsers = parsers.ToArray();
    private readonly IReadOnlyList<IMultiDocumentParser> _multiDocumentParsers = multiDocumentParsers.ToArray();

    public async IAsyncEnumerable<ParsedDocument> ParseAsync(
        string path,
        IReadOnlyDictionary<string, string>? attributes = null,
        string? contentType = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Structured parsers are offered the path first: they own the record-stream formats, and no
        // single-document parser claims those extensions.
        var multiParser = _multiDocumentParsers.FirstOrDefault(parser => parser.CanParse(path, contentType));
        if (multiParser is not null)
        {
            await foreach (var document in multiParser.ParseManyAsync(path, attributes, cancellationToken).ConfigureAwait(false))
            {
                yield return document;
            }

            yield break;
        }

        var parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(path, contentType))
            ?? throw new NotSupportedException($"No document parser is registered for '{path}'.");
        yield return await parser.ParseAsync(path, cancellationToken).ConfigureAwait(false);
    }
}
