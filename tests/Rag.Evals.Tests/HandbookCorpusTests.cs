using FluentAssertions;
using Rag.Evals;
using Rag.Evals.Corpus;
using Rag.Evals.Scoring;
using Xunit;

namespace Rag.Evals.Tests;

/// <summary>
/// Ties the committed <c>samples/handbook.pdf</c> to the source it is generated from.
///
/// The binary is committed and the generator is only reachable through a CLI command nothing in CI
/// runs, so without these the two drift apart in silence: a paragraph edited in
/// <see cref="HandbookContent"/> leaves every other test passing against the stale PDF, while the
/// file that claims to be the corpus's single source of truth no longer describes the corpus the
/// benchmark actually scores.
/// </summary>
public sealed class HandbookCorpusTests
{
    [Fact]
    public async Task CommittedHandbookMatchesTheContentItIsGeneratedFrom()
    {
        var parsed = TextNormalization.Normalize(await EvalApplication.ParseAsync(RepoPaths.HandbookPdf));

        foreach (var section in HandbookContent.Sections)
        {
            parsed.Should().Contain(
                TextNormalization.Normalize(section.Heading),
                "samples/handbook.pdf is stale; regenerate it with `dotnet run --project evals/Rag.Evals -- generate-handbook`");

            foreach (var paragraph in section.Paragraphs)
            {
                parsed.Should().Contain(
                    TextNormalization.Normalize(paragraph),
                    "samples/handbook.pdf is stale; regenerate it with `dotnet run --project evals/Rag.Evals -- generate-handbook`");
            }
        }
    }

    [Fact]
    public async Task GeneratedHandbookExtractsWithParagraphBreaksIntact()
    {
        // The layout constants exist to make PdfPig's baseline-gap heuristic emit blank lines. If a
        // spacing or font-size tweak ever flattens them, markdown-aware and semantic chunking
        // collapse to one chunk per document and the benchmark quietly becomes meaningless.
        var path = Path.Combine(Path.GetTempPath(), $"rag-evals-{Guid.NewGuid():N}", "handbook.pdf");
        try
        {
            HandbookPdfWriter.Write(path);

            var text = await EvalApplication.ParseAsync(path);

            text.Should().Contain("\n\n", "the extracted corpus must carry paragraph breaks");
            text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(50);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CommittedHandbookKeepsTheSentencePdfParserTestsAssertOn()
    {
        // tests/Rag.Core.Tests/PdfParserTests.cs asserts this exact substring to prove the quickstart
        // ingests extracted text rather than PDF syntax. It only holds while the sentence renders
        // unwrapped, which the writer enforces at generation time and this pins after the fact.
        var text = await EvalApplication.ParseAsync(RepoPaths.HandbookPdf);

        text.Should().Contain(HandbookContent.RequiredSentence);
        text.Split('\n')
            .Count(line => line.Contains(HandbookContent.RequiredSentence, StringComparison.Ordinal))
            .Should().Be(1, "the sentence must stay on a single line");
    }
}
