using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

public sealed class HtmlDocumentParser : IDocumentParser
{
    public string Name => "html";

    public bool CanParse(string path, string? contentType = null)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "text/html", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var html = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var metadata = ParserMetadata.ForFile(path, "text/html");
        return new ParsedDocument(metadata.DocumentId, HtmlTextExtractor.Extract(html), metadata);
    }
}
