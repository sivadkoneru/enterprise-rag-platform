using FluentAssertions;
using Rag.Evals.Dataset;
using Xunit;

namespace Rag.Evals.Tests;

public sealed class AnchorResolverTests
{
    [Fact]
    public void ResolvesAPhraseThatWrapsAcrossLinesInTheDocument()
    {
        // PDF extraction breaks sentences across lines, so the document contains a newline where the
        // authored phrase has a space. Resolution has to see through that or every multi-line
        // anchor would look like corpus drift.
        var resolver = new AnchorResolver("Intro.\n\nRefunds require a receipt\nwithin thirty days.\n\nEnd.");

        var (start, end) = resolver.Resolve("refunds require a receipt within thirty days.");

        start.Should().Be(8);
        end.Should().Be(53);
    }

    [Fact]
    public void ResolvedOffsetsPointAtTheOriginalText()
    {
        const string document = "Alpha beta.\n\nThe deadline is  sixty days from purchase.\n\nOmega.";
        var resolver = new AnchorResolver(document);

        var (start, end) = resolver.Resolve("the deadline is sixty days");

        // The document has a double space the phrase does not; the mapped range must still land on
        // real characters rather than drifting by the collapsed whitespace.
        document[start..end].Should().Be("The deadline is  sixty days");
    }

    [Fact]
    public void ThrowsWhenAPhraseIsMissingSoCorpusDriftIsLoud()
    {
        var resolver = new AnchorResolver("Nothing relevant here.");

        var act = () => resolver.Resolve("a phrase that was removed");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public void ThrowsWhenAPhraseIsAmbiguous()
    {
        // Two matches means "the chunk containing the evidence" has no single answer, which would
        // make recall and MRR quietly depend on which copy the retriever happened to return.
        var resolver = new AnchorResolver("within thirty days. Later: within thirty days.");

        var act = () => resolver.Resolve("within thirty days.");

        act.Should().Throw<InvalidOperationException>().WithMessage("*ambiguous*");
    }

    [Fact]
    public void NormalizationFoldsTypographicPunctuationToAscii()
    {
        var resolver = new AnchorResolver("The company’s policy — see below — applies.");

        var act = () => resolver.Resolve("the company's policy - see below - applies.");

        act.Should().NotThrow();
    }
}
