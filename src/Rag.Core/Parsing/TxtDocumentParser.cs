using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

public sealed class TxtDocumentParser : IDocumentParser
{
    public string Name => "txt";

    public bool CanParse(string path, string? contentType = null)
    {
        return string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "text/plain", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var metadata = ParserMetadata.ForFile(path, "text/plain");
        return new ParsedDocument(metadata.DocumentId, TextNormalizer.Normalize(text), metadata);
    }
}
