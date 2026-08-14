using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rag.Core.Parsing;
using Xunit;

namespace Rag.Core.Tests;

public sealed class PdfParserTests
{
    /// <summary>
    /// Records log entries so tests can assert on operator-visible diagnostics.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

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
            var document = await new PdfDocumentParser(NullLogger<PdfDocumentParser>.Instance).ParseAsync(path);

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

        var document = await new PdfDocumentParser(NullLogger<PdfDocumentParser>.Instance).ParseAsync(sample);

        document.Text.Should().Contain("refunds", "the documented `ingest ./samples` quickstart must succeed");
    }

    [Fact]
    public async Task RepositorySamplePdfExtractsTextWithoutPdfStructureTokens()
    {
        var sample = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/handbook.pdf"));
        if (!File.Exists(sample))
        {
            return; // Samples are not present in every packaging layout.
        }

        var document = await new PdfDocumentParser(NullLogger<PdfDocumentParser>.Instance).ParseAsync(sample);

        // The sample must be a structurally valid PDF so the quickstart exercises real text
        // extraction. A sample that only survives the raw-text fallback indexes PDF syntax as
        // document content, which silently poisons chunking, embedding, and citations.
        document.Text.Should().Contain("refunds require a receipt within thirty days.");
        document.Text.Should().NotContain("%PDF");
        document.Text.Should().NotContain("endobj");
        document.Text.Should().NotContain("/Type /Catalog");
    }

    [Fact]
    public async Task RawTextFallbackLogsWarningNamingTheDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(path, "%PDF-1.4\nBT (Refunds require a receipt.) Tj ET\n%%EOF");
        var logger = new CapturingLogger<PdfDocumentParser>();

        try
        {
            await new PdfDocumentParser(logger).ParseAsync(path);

            // Falling back still produces text, so without a warning the degraded extraction is
            // invisible: an operator sees a successful ingest full of PDF syntax.
            var warning = logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning).Subject;
            warning.Message.Should().Contain(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidPdfExtractionLogsNoWarning()
    {
        var sample = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/handbook.pdf"));
        if (!File.Exists(sample))
        {
            return; // Samples are not present in every packaging layout.
        }

        var logger = new CapturingLogger<PdfDocumentParser>();

        await new PdfDocumentParser(logger).ParseAsync(sample);

        logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Warning);
    }
}
