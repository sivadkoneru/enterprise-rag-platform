using System.Text;
using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Rag.Core.Parsing;

public sealed class PdfDocumentParser(ILogger<PdfDocumentParser> logger) : IDocumentParser
{
    /// <summary>
    /// Reconstructs line and paragraph breaks from letter geometry. <c>Page.Text</c> concatenates
    /// every letter with no separators at all, so it jams words together across line breaks
    /// ("thirty days.Section 4") and yields a document with no blank lines. Chunking strategies that
    /// split on paragraphs would then see one giant paragraph and emit a single chunk per document.
    /// </summary>
    private static readonly ContentOrderTextExtractor.Options TextExtractionOptions = new()
    {
        SeparateParagraphsWithDoubleNewline = true,
        ReplaceWhitespaceWithSpace = true
    };

    public string Name => "pdf";

    public bool CanParse(string path, string? contentType = null)
    {
        return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var extracted = ExtractWithPdfPig(path, out var failure);
        if (string.IsNullOrWhiteSpace(extracted))
        {
            // The fallback still returns text, so a degraded extraction would otherwise look like a
            // clean ingest. Warn so an operator can tell "indexed the document" from "indexed PDF
            // syntax" without diffing the stored chunks.
            logger.LogWarning(
                failure,
                "PdfPig extracted no text from '{Path}'; falling back to raw-text scraping, which can index PDF syntax instead of document content.",
                path);
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            extracted = ExtractReadableText(bytes);
        }

        var metadata = ParserMetadata.ForFile(path, "application/pdf");
        return new ParsedDocument(metadata.DocumentId, TextNormalizer.Normalize(extracted), metadata);
    }

    private string ExtractWithPdfPig(string path, out Exception? failure)
    {
        failure = null;
        try
        {
            using var document = PdfDocument.Open(path);
            var builder = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                var text = ContentOrderTextExtractor.GetText(page, TextExtractionOptions);
                if (string.IsNullOrWhiteSpace(text))
                {
                    // Layout analysis returns nothing for a page whose glyphs it cannot order
                    // (rotated text, unusual positioning). Page.Text has no separators at all, so
                    // it is a poor substitute — but dropping the page silently loses its content
                    // from an ingest that otherwise looks clean, so fall back and say so.
                    text = page.Text;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    logger.LogWarning(
                        "Layout analysis produced no text for page {PageNumber} of '{Path}'; falling back to unseparated page text, which has no line or paragraph breaks.",
                        page.Number,
                        path);
                }

                if (builder.Length > 0)
                {
                    // A page break is a paragraph break: the extractor's paragraph heuristic
                    // compares baseline gaps within a single page and cannot see across one.
                    builder.Append("\n\n");
                }

                builder.Append(text.Trim());
            }

            return builder.ToString();
        }
        // PdfPig raises library-specific exceptions for structurally invalid documents (missing
        // xref table, absent trailer /Size, and similar). Those are expected input, not faults:
        // returning empty hands the document to the raw-text fallback instead of failing ingestion.
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            failure = exception;
            return string.Empty;
        }
    }

    private static string ExtractReadableText(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var builder = new StringBuilder();
        foreach (var token in raw.Split(['\n', '\r', '\t', '\0'], StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = new string(token.Where(character => !char.IsControl(character)).ToArray()).Trim();
            if (cleaned.Length >= 3 && cleaned.Any(char.IsLetter))
            {
                builder.AppendLine(cleaned);
            }
        }

        return builder.Length == 0 ? "No extractable PDF text was found." : builder.ToString();
    }
}
