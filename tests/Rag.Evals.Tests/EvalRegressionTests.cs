using FluentAssertions;
using Xunit;

namespace Rag.Evals.Tests;

/// <summary>
/// The CI gate. Retrieval quality must not fall below the committed floors in
/// <c>evals/Rag.Evals/Data/thresholds.json</c>.
///
/// These run inside the ordinary <c>dotnet test</c> pass, so a change that quietly degrades
/// retrieval fails the build with the strategy and metric named, rather than shipping and being
/// noticed later in a regenerated table nobody reads.
/// </summary>
[Collection("evaluation")]
public sealed class EvalRegressionTests(EvalFixture fixture)
{
    [Fact]
    public void RetrievalMeetsTheCommittedFloorsForEveryStrategy()
    {
        foreach (var report in fixture.Run.Strategies)
        {
            fixture.Thresholds.Strategies.Should().ContainKey(report.Strategy);
            var floor = fixture.Thresholds.Strategies[report.Strategy];

            report.Recall5.Should().BeGreaterThanOrEqualTo(floor.Recall5,
                "recall@5 regressed for '{0}'", report.Strategy);
            report.Mrr5.Should().BeGreaterThanOrEqualTo(floor.Mrr5,
                "MRR@5 regressed for '{0}'", report.Strategy);
            report.CitationAccuracy1.Should().BeGreaterThanOrEqualTo(floor.CitationAccuracy1,
                "citation accuracy@1 regressed for '{0}'", report.Strategy);
            report.FullCoverage5.Should().BeGreaterThanOrEqualTo(floor.FullCoverage5,
                "full coverage@5 regressed for '{0}'", report.Strategy);
        }
    }

    [Fact]
    public void CitationsAlwaysDescribeTheChunksTheAnswerWasBuiltFrom()
    {
        // Citation integrity is an invariant, not a score: a citation that does not hydrate, or whose
        // index disagrees with its chunk, points the reader at something the model never saw.
        foreach (var report in fixture.Run.Strategies)
        {
            report.CitationIntegrity.Should().BeGreaterThanOrEqualTo(
                fixture.Thresholds.CitationIntegrityFloor,
                "citations must match their chunks exactly for '{0}'", report.Strategy);
        }
    }

    [Fact]
    public void AnswersStayGroundedInTheRetrievedContext()
    {
        // Under the deterministic client this holds by construction, which is the point: it fires if
        // the pipeline ever sends a prompt that disagrees with the citations it returns.
        foreach (var report in fixture.Run.Strategies)
        {
            report.Groundedness.Should().BeGreaterThanOrEqualTo(
                fixture.Thresholds.GroundednessFloor,
                "answers must stay grounded for '{0}'", report.Strategy);
        }
    }

    [Fact]
    public void EasyQuestionsRetrieveBetterThanHardOnes()
    {
        // If this inverts, the difficulty measure has stopped describing anything real and the
        // per-difficulty table in the README is noise.
        foreach (var report in fixture.Run.Strategies)
        {
            var easy = report.RecallByDifficulty["easy"];
            var hard = report.RecallByDifficulty["hard"];
            easy.Should().BeGreaterThan(hard, "difficulty must predict retrieval for '{0}'", report.Strategy);
        }
    }
}
