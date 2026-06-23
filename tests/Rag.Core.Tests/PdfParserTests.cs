using FluentAssertions;
using Rag.Core.Parsing;
using Xunit;

namespace Rag.Core.Tests;

public sealed class PdfParserTests
{
    [Fact]
    public async Task StructurallyInvalidPdfFallsBackToRawTextExtraction()
    {
        // A minimal hand-written PDF with no xref table and no trailer /Size: readable text,
        // but rejected by strict parsers. Ingestion must still yield its content.
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(path, """
            %PDF-1.4
            1 0 obj
            << /Type /Catalog /Pages 2 0 R >>
            endobj
            4 0 obj
            << /Length 78 >>
            stream
            BT /F1 12 Tf 72 720 Td (Refunds require a receipt within thirty days.) Tj ET
            endstream
            endobj
            trailer
            << /Root 1 0 R >>
            %%EOF
            """);

        try
        {
            var document = await new PdfDocumentParser().ParseAsync(path);

            document.Text.Should().Contain("Refunds require a receipt within thirty days.");
            document.Metadata.Extension.Should().Be("pdf");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RepositorySamplePdfIsIngestible()
    {
        var sample = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/handbook.pdf"));
        if (!File.Exists(sample))
        {
            return; // Samples are not present in every packaging layout.
        }

        var document = await new PdfDocumentParser().ParseAsync(sample);

        document.Text.Should().Contain("refunds", "the documented `ingest ./samples` quickstart must succeed");
    }
}
