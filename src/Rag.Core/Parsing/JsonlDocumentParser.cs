using System.IO.Compression;
using System.Text.Json;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

public sealed class JsonlDocumentParser : IMultiDocumentParser
{
    public string Name => "json";

    public bool CanParse(string path, string? contentType = null)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ndjson.gz", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "application/jsonl", StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<ParsedDocument> ParseManyAsync(
        string path,
        IReadOnlyDictionary<string, string>? attributes = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var format = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "jsonl";
        var profile = await StructuredSchemaLoader.LoadProfileAsync(path, format, attributes, cancellationToken).ConfigureAwait(false);
        if (format == "json")
        {
            await foreach (var document in ParseJsonAsync(path, attributes, profile, cancellationToken).ConfigureAwait(false))
            {
                yield return document;
            }

            yield break;
        }

        var skipped = 0;
        var lineNumber = 0;

        await using var file = File.OpenRead(path);
        using var gzip = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(file, CompressionMode.Decompress)
            : null;
        using var reader = new StreamReader(gzip is null ? file : gzip);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(line);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Invalid JSONL record at line {lineNumber} in '{path}': {exception.Message}", exception);
            }

            using (json)
            {
                var contentType = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? "application/gzip" : "application/x-ndjson";

                // Records stream out as they are read, so the skipped count is the running total
                // at this point in the file rather than the whole-file total.
                var document = StructuredRecordProjector.Project(
                    profile,
                    path,
                    lineNumber,
                    contentType,
                    "jsonl",
                    attributes,
                    skipped,
                    field => field.Path,
                    pointer => StructuredTextFormatter.JsonValue(json.RootElement, pointer));

                if (document is null)
                {
                    skipped++;
                    continue;
                }

                yield return document;
            }
        }
    }

    private static async IAsyncEnumerable<ParsedDocument> ParseJsonAsync(
        string path,
        IReadOnlyDictionary<string, string>? attributes,
        StructuredProfile profile,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var skipped = 0;
        var index = 0;

        foreach (var record in Records(json.RootElement))
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            // Records stream out as they are read, so the skipped count is the running total at
            // this point in the file rather than the whole-file total.
            var document = StructuredRecordProjector.Project(
                profile,
                path,
                index,
                "application/json",
                "json",
                attributes,
                skipped,
                field => field.Path,
                pointer => StructuredTextFormatter.JsonValue(record, pointer));

            if (document is null)
            {
                skipped++;
                continue;
            }

            yield return document;
        }
    }

    private static IEnumerable<JsonElement> Records(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    yield return item;
                }
            }

            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root;
        }
    }

}
