using FluentAssertions;
using System.IO.Compression;
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

    [Fact]
    public async Task HtmlParserExtractsVisibleText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(path, "<html><head><style>.x{}</style><script>alert(1)</script></head><body><h1>Title</h1><p>Hello <b>world</b>.</p><ul><li>One</li></ul><table><tr><td>A</td><td>B</td></tr></table></body></html>");

        var document = await new HtmlDocumentParser().ParseAsync(path);

        document.Text.Should().Contain("Title");
        document.Text.Should().Contain("Hello world");
        document.Text.Should().Contain("One");
        document.Text.Should().Contain("A");
        document.Text.Should().Contain("B");
        document.Text.Should().NotContain("alert");
        document.Text.Should().NotContain(".x");
    }

    [Fact]
    public async Task JsonlParserCreatesOneDocumentPerRecordUsingSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
        await File.WriteAllTextAsync(path, """
            {"id":"a","title":"Alpha","body":"# Refunds\nWithin thirty days.","category":"policy"}
            {"id":"skip","title":"Missing body"}
            {"title":"Beta","body":"<p>HTML body</p>","category":"faq"}
            """);
        await File.WriteAllTextAsync($"{path}.schema.json", """
            {
              "version": 1,
              "profiles": [
                {
                  "files": ["*.jsonl"],
                  "format": "jsonl",
                  "id": "/id",
                  "text": [
                    { "path": "/title", "format": "plain", "label": "Title" },
                    { "path": "/body", "format": "auto", "label": "Body", "required": true }
                  ],
                  "metadata": { "category": "/category" }
                }
              ]
            }
            """);

        var documents = await ReadAllAsync(new JsonlDocumentParser(), path);

        documents.Should().HaveCount(2);
        documents[0].Id.Should().Be("a");
        documents[0].Text.Should().Contain("Refunds");
        documents[0].Metadata.Attributes.Should().ContainKey("category").WhoseValue.Should().Be("policy");
        documents[0].Metadata.Attributes.Should().ContainKey("structuredSkippedRecords").WhoseValue.Should().Be("1");
        documents[1].Metadata.Attributes.Should().ContainKey("recordKey");
        documents[1].Text.Should().Contain("HTML body");
    }

    [Fact]
    public async Task JsonlParserReportsMalformedLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
        await File.WriteAllTextAsync(path, """
            {"body":"ok"}
            {"body":
            """);
        await File.WriteAllTextAsync($"{path}.schema.json", """
            { "version": 1, "profiles": [{ "files": ["*.jsonl"], "format": "jsonl", "text": [{ "path": "/body", "required": true }] }] }
            """);

        var act = () => ReadAllAsync(new JsonlDocumentParser(), path);

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*line 2*");
    }

    [Fact]
    public async Task JsonParserCreatesOneDocumentPerArrayItemUsingSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
            [
              { "id": "a", "title": "Alpha", "body": "Alpha body.", "url": "https://example.test/a" },
              { "id": "b", "title": "Beta", "body": "<p>Beta body.</p>", "url": "https://example.test/b" }
            ]
            """);
        await File.WriteAllTextAsync($"{path}.schema.json", """
            {
              "version": 1,
              "profiles": [
                {
                  "files": ["*.json"],
                  "format": "json",
                  "id": "/id",
                  "text": [
                    { "path": "/title", "format": "plain", "label": "Title" },
                    { "path": "/body", "format": "auto", "label": "Body", "required": true }
                  ],
                  "metadata": { "url": "/url" }
                }
              ]
            }
            """);

        var documents = await ReadAllAsync(new JsonlDocumentParser(), path);

        documents.Should().HaveCount(2);
        documents[0].Id.Should().Be("a");
        documents[1].Id.Should().Be("b");
        documents[1].Text.Should().Contain("Beta body.");
        documents[1].Metadata.Attributes.Should().ContainKey("structuredFormat").WhoseValue.Should().Be("json");
        documents[1].Metadata.Attributes.Should().ContainKey("url").WhoseValue.Should().Be("https://example.test/b");
    }

    [Fact]
    public async Task JsonParserCreatesSingleDocumentFromObjectUsingJsonlCompatibleSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """{ "id": "single", "body": "Single JSON object." }""");
        await File.WriteAllTextAsync($"{path}.schema.json", """
            { "version": 1, "profiles": [{ "files": ["*.json"], "format": "jsonl", "id": "/id", "text": [{ "path": "/body", "required": true }] }] }
            """);

        var documents = await ReadAllAsync(new JsonlDocumentParser(), path);

        documents.Should().ContainSingle();
        documents[0].Id.Should().Be("single");
        documents[0].Text.Should().Be("Single JSON object.");
    }

    [Fact]
    public async Task JsonlParserReadsGzipAndNaturalQuestionsShape()
    {
        var path = Path.Combine(Path.GetTempPath(), $"simplified-nq-{Guid.NewGuid():N}.jsonl.gz");
        await using (var file = File.Create(path))
        await using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        await using (var writer = new StreamWriter(gzip))
        {
            await writer.WriteLineAsync("""{"example_id":"nq-1","question_text":"What is Alpha?","document_html":"<html><body><h1>Alpha</h1><p>Alpha is first.</p></body></html>","document_url":"https://example.test/alpha"}""");
        }

        await File.WriteAllTextAsync($"{path}.schema.json", """
            {
              "version": 1,
              "profiles": [
                {
                  "files": ["simplified-nq-*.jsonl.gz"],
                  "format": "jsonl",
                  "id": "/example_id",
                  "text": [
                    { "path": "/question_text", "format": "plain", "label": "Question" },
                    { "path": "/document_html", "format": "html", "label": "Wikipedia Document", "required": true }
                  ],
                  "metadata": { "documentUrl": "/document_url" }
                }
              ]
            }
            """);

        var documents = await ReadAllAsync(new JsonlDocumentParser(), path);

        documents.Should().ContainSingle();
        documents[0].Id.Should().Be("nq-1");
        documents[0].Text.Should().Contain("What is Alpha?");
        documents[0].Text.Should().Contain("Alpha is first.");
        documents[0].Metadata.Attributes.Should().ContainKey("documentUrl").WhoseValue.Should().Be("https://example.test/alpha");
    }

    [Fact]
    public async Task CsvParserHandlesQuotedMultilineRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "id,title,content,department\n1,Alpha,\"First line\nSecond line\",ops\n2,Beta,<p>HTML value</p>,support\n");
        await File.WriteAllTextAsync($"{path}.schema.json", """
            {
              "version": 1,
              "profiles": [
                {
                  "files": ["*.csv"],
                  "format": "csv",
                  "id": "id",
                  "text": [
                    { "column": "title", "format": "plain", "label": "Title" },
                    { "column": "content", "format": "auto", "label": "Content", "required": true }
                  ],
                  "metadata": { "department": "department" }
                }
              ]
            }
            """);

        var documents = await ReadAllAsync(new CsvDocumentParser(), path);

        documents.Should().HaveCount(2);
        documents[0].Id.Should().Be("1");
        documents[0].Text.Should().Contain("First line\nSecond line");
        documents[1].Text.Should().Contain("HTML value");
        documents[1].Metadata.Attributes.Should().ContainKey("department").WhoseValue.Should().Be("support");
    }

    [Fact]
    public async Task CsvParserReportsMissingRequiredColumns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "id,title\n1,Alpha\n");
        await File.WriteAllTextAsync($"{path}.schema.json", """
            { "version": 1, "profiles": [{ "files": ["*.csv"], "format": "csv", "text": [{ "column": "body", "required": true }] }] }
            """);

        var act = () => ReadAllAsync(new CsvDocumentParser(), path);

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*body*");
    }

    private static async Task<List<Rag.Core.Models.ParsedDocument>> ReadAllAsync(
        Rag.Core.Abstractions.IMultiDocumentParser parser,
        string path)
    {
        var documents = new List<Rag.Core.Models.ParsedDocument>();
        await foreach (var document in parser.ParseManyAsync(path))
        {
            documents.Add(document);
        }

        return documents;
    }
}
