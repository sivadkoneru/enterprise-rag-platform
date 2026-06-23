using System.Collections;
using System.Reflection;
using FluentAssertions;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Vector;
using Xunit;

namespace Rag.Core.Tests;

public sealed class VectorFilterContractTests
{
    [Fact]
    public void VectorSearchFilterIsPartOfTheSearchContract()
    {
        var filterType = RequiredType("Rag.Core.Models.VectorSearchFilter");

        var search = typeof(IVectorStore).GetMethods()
            .Where(method => method.Name == nameof(IVectorStore.SearchAsync))
            .Should()
            .ContainSingle(method => method.GetParameters().Any(parameter => parameter.ParameterType == filterType))
            .Subject;

        search.GetParameters().Should().Contain(parameter => parameter.Name == "filter" && parameter.ParameterType == filterType);
        filterType.GetProperty("DocumentIds").Should().NotBeNull();
        filterType.GetProperty("Sources").Should().NotBeNull();
        filterType.GetProperty("Origins").Should().NotBeNull();
        filterType.GetProperty("FileTypes").Should().NotBeNull();
    }

    [Fact]
    public async Task InMemoryVectorStoreAppliesDocumentOriginSourceAndFileTypeFilters()
    {
        var filterType = RequiredType("Rag.Core.Models.VectorSearchFilter");
        var search = typeof(IVectorStore).GetMethods()
            .Single(method => method.Name == nameof(IVectorStore.SearchAsync) &&
                method.GetParameters().Any(parameter => parameter.ParameterType == filterType));
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(
            [
                Record("chunk-file-txt", "doc-file", [1, 0], "file", "file:///docs/refund.txt", ".txt"),
                Record("chunk-s3-pdf", "doc-s3", [1, 0], "s3", "s3://rag-docs/refund.pdf", ".pdf"),
                Record("chunk-blob-md", "doc-blob", [1, 0], "azureblob", "azureblob://rag-docs/refund.md", ".md")
            ]);

        var filter = CreateFilter(filterType, documentIds: ["doc-s3"], sources: ["s3://rag-docs/refund.pdf"], origins: ["s3"], fileTypes: [".pdf"]);
        var results = await InvokeSearchAsync(search, store, [1, 0], 10, filter);

        results.Should().ContainSingle();
        GetStringProperty(results.Single(), "DocumentId").Should().Be("doc-s3");
        GetStringProperty(results.Single(), "ChunkId").Should().Be("chunk-s3-pdf");
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

    private static async Task<IReadOnlyList<object>> InvokeSearchAsync(MethodInfo search, object store, IReadOnlyList<float> queryVector, int topK, object filter)
    {
        var parameters = search.GetParameters();
        var arguments = parameters.Select(parameter =>
        {
            if (parameter.ParameterType == typeof(int))
            {
                return (object)topK;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                return CancellationToken.None;
            }

            if (parameter.ParameterType.IsInstanceOfType(filter))
            {
                return filter;
            }

            return queryVector;
        }).ToArray();

        var task = (Task)search.Invoke(store, arguments)!;
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        return ((IEnumerable)result!).Cast<object>().ToArray();
    }

    private static object CreateFilter(Type filterType, IReadOnlyList<string> documentIds, IReadOnlyList<string> sources, IReadOnlyList<string> origins, IReadOnlyList<string> fileTypes)
    {
        var constructor = filterType.GetConstructors()
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();
        constructor.Should().NotBeNull("VectorSearchFilter should be constructible in tests and API binding");

        var values = constructor!.GetParameters().Select(parameter =>
        {
            return parameter.Name switch
            {
                "documentIds" or "DocumentIds" => documentIds,
                "sources" or "Sources" => sources,
                "origins" or "Origins" => origins,
                "fileTypes" or "FileTypes" => fileTypes,
                _ => null
            };
        }).ToArray();

        return constructor.Invoke(values);
    }

    private static string? GetStringProperty(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance) as string;
    }

    private static Type RequiredType(string fullName)
    {
        var type = typeof(VectorRecord).Assembly.GetType(fullName);
        type.Should().NotBeNull($"metadata-scoped retrieval requires public contract {fullName}");
        return type!;
    }
}
