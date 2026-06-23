using FluentAssertions;
using Rag.Core.Common;
using Xunit;

namespace Rag.Core.Tests;

public sealed class FileExtensionsTests
{
    [Fact]
    public void Normalize_PlainExtension_IsLowercased()
    {
        var extension = FileExtensions.Normalize("document.TXT");

        extension.Should().Be(".txt");
    }

    [Fact]
    public void Normalize_JsonlGzSuffix_ReturnsCompoundExtension()
    {
        var extension = FileExtensions.Normalize("records.jsonl.gz");

        extension.Should().Be(".jsonl.gz");
    }

    [Fact]
    public void Normalize_NdjsonGzSuffix_ReturnsCompoundExtension()
    {
        var extension = FileExtensions.Normalize("records.ndjson.gz");

        extension.Should().Be(".ndjson.gz");
    }

    [Fact]
    public void Normalize_ExtensionLessName_ReturnsEmptyString()
    {
        var extension = FileExtensions.Normalize("README");

        extension.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_MixedCase_IsCaseInsensitive()
    {
        var extension = FileExtensions.Normalize("Archive.NdJsOn.Gz");

        extension.Should().Be(".ndjson.gz");
    }
}
