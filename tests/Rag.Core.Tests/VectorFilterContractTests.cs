using FluentAssertions;
using Rag.Core.Models;
using Rag.Core.Vector;
using Xunit;

namespace Rag.Core.Tests;

public sealed class VectorFilterContractTests
{
    [Fact]
    public async Task InMemoryVectorStoreAppliesDocumentOriginSourceAndFileTypeFilters()
    {
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(
            [
                Record("chunk-file-txt", "doc-file", [1, 0], "file", "file:///docs/refund.txt", ".txt"),
                Record("chunk-s3-pdf", "doc-s3", [1, 0], "s3", "s3://rag-docs/refund.pdf", ".pdf"),
                Record("chunk-blob-md", "doc-blob", [1, 0], "azureblob", "azureblob://rag-docs/refund.md", ".md")
            ]);

        var results = await store.SearchAsync(
            [1, 0],
            10,
            new VectorSearchFilter(
                DocumentIds: ["doc-s3"],
                Sources: ["s3://rag-docs/refund.pdf"],
                Origins: ["s3"],
                FileTypes: [".pdf"]));

        results.Should().ContainSingle();
        results.Single().DocumentId.Should().Be("doc-s3");
        results.Single().ChunkId.Should().Be("chunk-s3-pdf");
    }

    [Fact]
    public async Task FilterFieldsAreCombinedWithAndSoASourceValueCannotDoubleAsAnOrigin()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([Record("chunk-s3-pdf", "doc-s3", [1, 0], "s3", "s3://rag-docs/refund.pdf", ".pdf")]);

        var byOrigin = await store.SearchAsync([1, 0], 10, new VectorSearchFilter(Origins: ["s3"]));
        var bySource = await store.SearchAsync([1, 0], 10, new VectorSearchFilter(Sources: ["s3://rag-docs/refund.pdf"]));
        var originValueUsedAsSource = await store.SearchAsync([1, 0], 10, new VectorSearchFilter(Sources: ["s3"], Origins: ["s3"]));

        byOrigin.Should().ContainSingle();
        bySource.Should().ContainSingle();
        originValueUsedAsSource.Should().BeEmpty("every populated filter field must match, so callers must not map one flag onto two fields");
    }

    [Fact]
    public async Task FileTypeFiltersIgnoreLeadingDotAndCase()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([Record("chunk-file-txt", "doc-file", [1, 0], "file", "file:///docs/refund.txt", ".txt")]);

        var results = await store.SearchAsync([1, 0], 10, new VectorSearchFilter(FileTypes: ["TXT"]));

        results.Should().ContainSingle();
    }

    private static VectorRecord Record(string chunkId, string documentId, IReadOnlyList<float> vector, string origin, string source, string fileType)
    {
        return new VectorRecord(
            chunkId,
            documentId,
            vector,
            new Dictionary<string, string>
            {
                ["origin"] = origin,
                ["source"] = source,
                ["fileType"] = fileType
            });
    }

}
