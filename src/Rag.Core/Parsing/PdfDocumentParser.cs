using System.Text;
using Microsoft.Extensions.Logging;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using UglyToad.PdfPig;

namespace Rag.Core.Parsing;

public sealed class PdfDocumentParser(ILogger<PdfDocumentParser> logger) : IDocumentParser
{
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

    private static string ExtractWithPdfPig(string path, out Exception? failure)
    {
        failure = null;
        try
        {
            using var document = PdfDocument.Open(path);
            var builder = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
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
