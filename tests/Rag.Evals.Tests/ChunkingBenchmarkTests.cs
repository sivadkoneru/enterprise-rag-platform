using FluentAssertions;
using Rag.Evals.Composition;
using Xunit;

namespace Rag.Evals.Tests;

/// <summary>
/// Invariants the four-way benchmark depends on. Without these the published table can silently
/// become meaningless while every other test stays green.
/// </summary>
[Collection("evaluation")]
public sealed class ChunkingBenchmarkTests(EvalFixture fixture)
{
    [Fact]
    public void AllFourStrategiesAreMeasured()
    {
        fixture.Run.Strategies.Select(report => report.Strategy)
            .Should().BeEquivalentTo(EvalRunner.Strategies);
    }

    [Fact]
    public void NoStrategyCollapsesToASingleChunk()
    {
        // This is the regression test for PDF text extraction. Markdown-aware and semantic chunking
        // split on blank lines, so if PdfDocumentParser ever stops reconstructing paragraph breaks
        // both silently emit one chunk per document, score a perfect trivial recall, and turn the
        // benchmark into a lie. Fail loudly here instead.
        foreach (var report in fixture.Run.Strategies)
        {
            report.ChunkCount.Should().BeGreaterThanOrEqualTo(
                fixture.Thresholds.MinChunksPerStrategy,
                "strategy '{0}' produced {1} chunk(s); a strategy that emits one chunk per document " +
                "is not chunking, which usually means the parsed corpus lost its paragraph breaks",
                report.Strategy,
                report.ChunkCount);
        }
    }

    [Fact]
    public void StrategiesProduceGenuinelyDifferentChunkings()
    {
        // Four identical chunkings would make the comparison meaningless even with plenty of chunks.
        var signatures = fixture.Run.Strategies
            .Select(report => (report.ChunkCount, report.AverageChunkChars))
            .Distinct()
            .ToArray();

        signatures.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void SemanticChunkingCostsMoreIndexEmbeddingsThanTheRest()
    {
        // Semantic chunking embeds every paragraph to decide where to cut, so its index cost is
        // structurally higher. If this ever inverts, the counter is measuring the wrong thing.
        var semantic = fixture.Run.Strategies.Single(report => report.Strategy == "semantic");
        var others = fixture.Run.Strategies.Where(report => report.Strategy != "semantic");

        semantic.IndexEmbedCalls.Should().BeGreaterThan(others.Max(report => report.IndexEmbedCalls));
    }

    [Fact]
    public void EveryStrategyRetrievesSomethingForEveryQuestion()
    {
        foreach (var report in fixture.Run.Strategies)
        {
            report.Outcomes.Should().HaveCount(fixture.Run.QuestionCount);
            report.Outcomes.Should().OnlyContain(outcome => outcome.RetrievedChunks.Count > 0);
        }
    }
}
