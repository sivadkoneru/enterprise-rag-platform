using FluentAssertions;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

/// <summary>
/// Covers the shared <see cref="VectorSearchFilter.FromLists"/> factory that both the API
/// (<c>ApiQueryFilter.ToCoreFilter</c>) and the CLI (<c>query</c> command) call so their filter
/// normalization cannot drift apart.
/// </summary>
public sealed class QueryFilterTests
{
    [Fact]
    public void FromLists_ReturnsNullWhenAllListsAreNull()
    {
        VectorSearchFilter.FromLists(null, null, null, null).Should().BeNull();
    }

    [Fact]
    public void FromLists_ReturnsNullWhenAllListsAreEmpty()
    {
        VectorSearchFilter.FromLists([], [], [], []).Should().BeNull();
    }

    [Fact]
    public void FromLists_NormalizesEmptyOrNullListsToNullPerField()
    {
        var filter = VectorSearchFilter.FromLists(
            documentIds: ["doc-1"],
            sources: [],
            origins: null,
            fileTypes: []);

        filter.Should().NotBeNull();
        filter!.DocumentIds.Should().Equal("doc-1");
        filter.Sources.Should().BeNull();
        filter.Origins.Should().BeNull();
        filter.FileTypes.Should().BeNull();
    }

    [Fact]
    public void FromLists_PopulatedFieldsSurvive()
    {
        var filter = VectorSearchFilter.FromLists(
            documentIds: ["doc-1", "doc-2"],
            sources: ["s3://rag-docs/refund.pdf"],
            origins: ["s3"],
            fileTypes: [".pdf"]);

        filter.Should().NotBeNull();
        filter!.DocumentIds.Should().Equal("doc-1", "doc-2");
        filter.Sources.Should().Equal("s3://rag-docs/refund.pdf");
        filter.Origins.Should().Equal("s3");
        filter.FileTypes.Should().Equal(".pdf");
    }
}
