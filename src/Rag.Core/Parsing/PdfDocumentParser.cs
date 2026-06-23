using System.Text;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using UglyToad.PdfPig;

namespace Rag.Core.Parsing;

public sealed class PdfDocumentParser : IDocumentParser
{
    public string Name => "pdf";

    public bool CanParse(string path, string? contentType = null)
    {
        return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var extracted = ExtractWithPdfPig(path);
        if (string.IsNullOrWhiteSpace(extracted))
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            extracted = ExtractReadableText(bytes);
        }

        var metadata = ParserMetadata.ForFile(path, "application/pdf");
        return new ParsedDocument(metadata.DocumentId, TextNormalizer.Normalize(extracted), metadata);
    }

    private static string ExtractWithPdfPig(string path)
    {
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
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
