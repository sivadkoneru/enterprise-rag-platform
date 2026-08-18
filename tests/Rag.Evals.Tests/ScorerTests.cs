using FluentAssertions;
using Rag.Core.Models;
using Rag.Evals.Dataset;
using Rag.Evals.Scoring;
using Xunit;

namespace Rag.Evals.Tests;

public sealed class ScorerTests
{
    private static readonly DocumentMetadata Metadata =
        new("doc", "/corpus/handbook.pdf", "handbook.pdf", "pdf", "application/pdf", 100, DateTimeOffset.UnixEpoch);

    private static TextChunk Chunk(string text, int start, int end, int index = 0) =>
        new($"doc:test:{index:D5}", "doc", index, text, start, end, Metadata);

    private static ResolvedAnchor Anchor(string phrase, int start, int end) =>
        new(new GoldAnchor(phrase, "section"), start, end);

    [Fact]
    public void CoversIgnoresWhitespaceDifferencesBetweenChunkAndPhrase()
    {
        var chunk = Chunk("Claims must be submitted\nwithin sixty days of the cost.", 0, 56);

        Scorers.Covers(chunk, Anchor("submitted within sixty days", 25, 52)).Should().BeTrue();
    }

    [Fact]
    public void UnionCoversRescuesEvidenceSplitAcrossAdjacentChunks()
    {
        // Fixed-size chunking cuts mid-sentence, so a phrase routinely straddles a boundary. Neither
        // chunk contains it, but together they delivered every character of it to the prompt.
        var first = Chunk("Claims must be submitted within", 0, 30, 0);
        var second = Chunk(" sixty days of the cost.", 30, 54, 1);
        var anchor = Anchor("submitted within sixty days", 15, 46);

        Scorers.Covers(first, anchor).Should().BeFalse();
        Scorers.Covers(second, anchor).Should().BeFalse();
        Scorers.UnionCovers([first, second], anchor).Should().BeTrue();
        Scorers.Found([first, second], anchor).Should().BeTrue();
    }

    [Fact]
    public void UnionCoversRejectsANonContiguousRetrievedSet()
    {
        // A gap between the two chunks means the middle of the evidence was never retrieved.
        var first = Chunk("Claims must be submitted within", 0, 30, 0);
        var distant = Chunk("Unrelated policy text here.", 400, 427, 1);

        Scorers.UnionCovers([first, distant], Anchor("submitted within sixty days", 15, 46)).Should().BeFalse();
    }

    [Fact]
    public void FirstRelevantRankIsOneBasedAndZeroWhenNothingMatches()
    {
        var hit = Chunk("within sixty days", 0, 17, 1);
        var miss = Chunk("nothing useful", 20, 34, 0);
        var anchor = Anchor("within sixty days", 0, 17);

        Scorers.FirstRelevantRank([miss, hit], [anchor]).Should().Be(2);
        Scorers.FirstRelevantRank([miss], [anchor]).Should().Be(0);
    }

    [Fact]
    public void GroundednessStripsTheDeterministicPrefixBeforeScoring()
    {
        // Without stripping, the client's own boilerplate would count as grounded content and lift
        // the score of an answer that is otherwise entirely invented.
        var chunk = Chunk("Refunds require a receipt.", 0, 26);

        var (score, ungrounded) = Scorers.Groundedness(
            "Based on the retrieved context, quantum wombats authorise refunds.", [chunk]);

        score.Should().BeLessThan(1);
        ungrounded.Should().Contain("wombats");
    }

    [Fact]
    public void GroundednessIsOneWhenEveryAnswerTokenAppearsInContext()
    {
        var chunk = Chunk("Refunds require a receipt within thirty days.", 0, 44);

        var (score, ungrounded) = Scorers.Groundedness("Refunds require a receipt.", [chunk]);

        score.Should().Be(1);
        ungrounded.Should().BeEmpty();
    }

    [Fact]
    public void SupportZScoreIsScaleFree()
    {
        // The same shape at ten times the magnitude must score identically, which is the property
        // that makes the number comparable across strategies with different chunk sizes.
        var small = Scorers.SupportZScore([0.9, 0.1, 0.1, 0.1]);
        var large = Scorers.SupportZScore([9.0, 1.0, 1.0, 1.0]);

        small.Should().BeApproximately(large, 1e-9);
        small.Should().BeGreaterThan(1);
    }

    [Fact]
    public void SupportZScoreIsZeroWhenNothingStandsOut()
    {
        Scorers.SupportZScore([0.5, 0.5, 0.5, 0.5]).Should().Be(0);
    }

    [Fact]
    public void AnswerKeywordsRequireEveryKeyword()
    {
        Scorers.AnswerKeywordsHit("Paid within twenty-eight days.", ["twenty-eight days"]).Should().BeTrue();
        Scorers.AnswerKeywordsHit("Paid within twenty-eight days.", ["twenty-eight days", "director"]).Should().BeFalse();
    }
}
