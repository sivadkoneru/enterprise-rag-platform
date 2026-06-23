using FluentAssertions;
using Rag.Core.Common;
using Xunit;

namespace Rag.Core.Tests;

public sealed class CommonPrimitivesTests
{
    [Fact]
    public void StableId_Compute_IsDeterministic()
    {
        var first = StableId.Compute("origin:source:record");
        var second = StableId.Compute("origin:source:record");

        first.Should().Be(second);
    }

    [Fact]
    public void StableId_Compute_Returns24CharacterLowercaseHex()
    {
        var id = StableId.Compute("origin:source:record");

        id.Should().HaveLength(24);
        id.Should().MatchRegex("^[0-9a-f]{24}$");
    }

    [Fact]
    public void StableId_Compute_DiffersForDifferentInputs()
    {
        var first = StableId.Compute("origin:source:record-a");
        var second = StableId.Compute("origin:source:record-b");

        first.Should().NotBe(second);
    }

    [Fact]
    public void CosineSimilarity_ReturnsOneForIdenticalVectors()
    {
        var left = new[] { 1f, 2f, 3f };
        var right = new[] { 1f, 2f, 3f };

        var similarity = VectorMath.CosineSimilarity(left, right);

        similarity.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void CosineSimilarity_ReturnsZeroForOrthogonalVectors()
    {
        var left = new[] { 1f, 0f };
        var right = new[] { 0f, 1f };

        var similarity = VectorMath.CosineSimilarity(left, right);

        similarity.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void CosineSimilarity_ReturnsZeroWhenAVectorIsAllZero()
    {
        var left = new[] { 0f, 0f, 0f };
        var right = new[] { 1f, 2f, 3f };

        var similarity = VectorMath.CosineSimilarity(left, right);

        similarity.Should().Be(0);
    }

    [Fact]
    public void CosineSimilarity_DifferingLengths_UsesTheShorterVector()
    {
        // If the extra trailing element on "right" were included, the huge magnitude it
        // introduces would pull similarity far away from 1. Getting ~1 back proves only the
        // shared (shorter) length was compared.
        var left = new[] { 1f, 1f };
        var right = new[] { 1f, 1f, 100f };

        var similarity = VectorMath.CosineSimilarity(left, right);

        similarity.Should().BeApproximately(1.0, 1e-9);
    }
}
