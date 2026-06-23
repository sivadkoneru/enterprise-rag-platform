using FluentAssertions;
using Rag.Core.Parsing;
using Xunit;

namespace Rag.Core.Tests;

public sealed class ParserTests
{
    [Fact]
    public async Task TxtParserNormalizesPlainText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "Hello\r\n\r\n\r\nworld");

        var document = await new TxtDocumentParser().ParseAsync(path);

        document.Text.Should().Be("Hello\n\nworld");
        document.Metadata.Extension.Should().Be("txt");
    }

    [Fact]
    public async Task MarkdownParserStripsCommonMarkup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "# Title\n\nA [link](https://example.com).");

        var document = await new MarkdownDocumentParser().ParseAsync(path);

        document.Text.Should().Contain("Title");
        document.Text.Should().Contain("link");
        document.Text.Should().NotContain("https://example.com");
    }
}
