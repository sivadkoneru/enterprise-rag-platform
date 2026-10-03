using FluentAssertions;
using Rag.Core.Workbench;
using Rag.LiveEvals;
using Xunit;

namespace Rag.Evals.Tests;

public sealed class LiveScoringTests
{
    [Fact]
    public void SourceQualifiedRankingsDistinguishConflictingPolicies()
    {
        var question = new EvaluationCase("refund", "held-out", "conflict", "Refund?", true, [new("direct.txt", 0, 16, "Refund in 30 days")], ["Refund in 30 days"]);
        DetailedCandidate[] ranking = [Chunk("wrong", "reseller.txt", "Refund in 30 days"), Chunk("right", "direct.txt", "Refund in 30 days")];
        Rag.LiveEvals.Scoring.Retrieval(question, ranking, ranking, 1).Recall.Should().Be(0);
        var result = Rag.LiveEvals.Scoring.Retrieval(question, ranking, ranking, 3);
        result.Recall.Should().Be(1);
        result.Mrr.Should().Be(.5);
        result.Ndcg.Should().BeApproximately(1 / Math.Log2(3), .00001);
    }

    [Fact]
    public void FragmentedEvidenceIsDiagnosedWithoutClaimingSingleChunkRecall()
    {
        var question = new EvaluationCase("split", "held-out", "synthesis", "Policy?", true, [new("policy.txt", 0, 30, "Thirty characters of evidence.")], []);
        DetailedCandidate[] ranking = [Chunk("left", "policy.txt", "Thirty characters"), Chunk("right", "policy.txt", "of evidence.")];
        var spans = new Dictionary<string, ChunkSpan> { ["left"] = new("policy.txt", 0, 15), ["right"] = new("policy.txt", 15, 30) };
        var result = Rag.LiveEvals.Scoring.Retrieval(question, ranking, ranking, 3, spans);
        result.Recall.Should().Be(0);
        result.Mrr.Should().Be(0);
        result.FragmentedEvidence.Should().Be(1);
        result.Ndcg.Should().Be(1);
    }

    [Fact]
    public void RepeatedWordsAtAnotherSourceSpanDoNotRecoverTheGoldAnchor()
    {
        var question = new EvaluationCase("repeated", "held-out", "lookup", "Policy?", true, [new("policy.txt", 100, 116, "Refund in 30 days")], []);
        DetailedCandidate[] ranking = [Chunk("other-section", "policy.txt", "Refund in 30 days")];
        var spans = new Dictionary<string, ChunkSpan> { ["other-section"] = new("policy.txt", 0, 16) };
        var result = Rag.LiveEvals.Scoring.Retrieval(question, ranking, ranking, 1, spans);
        result.Recall.Should().Be(0);
        result.Mrr.Should().Be(0);
        result.Ndcg.Should().BeNull();
    }

    [Fact]
    public void UnanswerableRetrievalMetricsAreMissingNotZero()
    {
        var result = Rag.LiveEvals.Scoring.Retrieval(new EvaluationCase("unknown", "held-out", "unsupported", "Unknown?", false, [], []), [], [], 5);
        result.Recall.Should().BeNull();
        result.MissingReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ExhaustedBudgetDoesNotAdmitAnotherRequest()
    {
        var budget = new RunBudget(10, 2);
        budget.TryReserve(7).Should().BeTrue();
        budget.TryReserve(2).Should().BeFalse();
        budget.Reserved.Should().Be(9);
        budget.TryReserve(1).Should().BeTrue();
        budget.TryReserve(.001m).Should().BeFalse();
    }

    [Fact]
    public void MissingLatencyIsNullAndPercentilesUseNearestRank()
    {
        Rag.LiveEvals.Scoring.Percentile([], .95).Should().BeNull();
        Rag.LiveEvals.Scoring.Percentile([10, 30, 20, 40], .5).Should().Be(20);
        Rag.LiveEvals.Scoring.Percentile([10, 30, 20, 40], .95).Should().Be(40);
    }

    private static DetailedCandidate Chunk(string id, string source, string text) => new(id, "doc", source, 0, text, null, null, null, null, 0, 0, true);
}
