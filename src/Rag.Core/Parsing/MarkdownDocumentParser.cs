using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

public sealed partial class MarkdownDocumentParser : IDocumentParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

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
        var html = Markdown.ToHtml(markdown, Pipeline);
        var plainText = HtmlTags().Replace(html, " ");
        return new ParsedDocument(metadata.DocumentId, TextNormalizer.Normalize(WebUtility.HtmlDecode(plainText)), metadata);
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTags();
}
