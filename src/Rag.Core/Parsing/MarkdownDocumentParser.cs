using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

public sealed class MarkdownDocumentParser : IDocumentParser
{
    public string Name => "markdown";

    public bool CanParse(string path, string? contentType = null)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "text/markdown", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var metadata = ParserMetadata.ForFile(path, "text/markdown");
        return new ParsedDocument(metadata.DocumentId, TextNormalizer.StripMarkdown(markdown), metadata);
    }
}
