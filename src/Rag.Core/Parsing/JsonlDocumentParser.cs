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
            foreach (var document in await ParseJsonAsync(path, attributes, profile, cancellationToken).ConfigureAwait(false))
            {
                await Task.Yield();
                yield return document;
            }

            yield break;
        }

        var documents = new List<ParsedDocument>();
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
                var text = StructuredTextFormatter.BuildText(
                    profile.Text!.Select(field => (field, StructuredTextFormatter.JsonValue(json.RootElement, field.Path))),
                    out var missingRequired);

                if (missingRequired || string.IsNullOrWhiteSpace(text))
                {
                    skipped++;
                    continue;
                }

                var configuredId = StructuredTextFormatter.JsonValue(json.RootElement, profile.Id);
                var recordKey = StructuredDocumentIds.RecordKey(configuredId, path, lineNumber, text);
                var metadata = ParserMetadata.ForFile(path, path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? "application/gzip" : "application/x-ndjson");
                var parsedAttributes = Attributes(attributes, profile, json.RootElement, lineNumber, recordKey, "jsonl");
                documents.Add(new ParsedDocument(recordKey, text, metadata with { DocumentId = recordKey, Attributes = parsedAttributes }));
            }
        }

        foreach (var document in documents)
        {
            await Task.Yield();
            yield return document with
            {
                Metadata = document.Metadata with
                {
                    Attributes = WithSkippedCount(document.Metadata.Attributes, skipped)
                }
            };
        }
    }

    private static async Task<IReadOnlyList<ParsedDocument>> ParseJsonAsync(
        string path,
        IReadOnlyDictionary<string, string>? attributes,
        StructuredProfile profile,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var documents = new List<ParsedDocument>();
        var skipped = 0;
        var index = 0;

        foreach (var record in Records(json.RootElement))
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            var text = StructuredTextFormatter.BuildText(
                profile.Text!.Select(field => (field, StructuredTextFormatter.JsonValue(record, field.Path))),
                out var missingRequired);

            if (missingRequired || string.IsNullOrWhiteSpace(text))
            {
                skipped++;
                continue;
            }

            var configuredId = StructuredTextFormatter.JsonValue(record, profile.Id);
            var recordKey = StructuredDocumentIds.RecordKey(configuredId, path, index, text);
            var metadata = ParserMetadata.ForFile(path, "application/json");
            var parsedAttributes = Attributes(attributes, profile, record, index, recordKey, "json");
            documents.Add(new ParsedDocument(recordKey, text, metadata with { DocumentId = recordKey, Attributes = parsedAttributes }));
        }

        return documents.Select(document => document with
        {
            Metadata = document.Metadata with
            {
                Attributes = WithSkippedCount(document.Metadata.Attributes, skipped)
            }
        }).ToArray();
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

    private static IReadOnlyDictionary<string, string> Attributes(
        IReadOnlyDictionary<string, string>? sourceAttributes,
        StructuredProfile profile,
        JsonElement root,
        int recordIndex,
        string recordKey,
        string format)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recordIndex"] = recordIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["recordKey"] = recordKey,
            ["structuredFormat"] = format
        };

        CopySourceAttributes(sourceAttributes, attributes);
        if (profile.Metadata is not null)
        {
            foreach (var item in profile.Metadata)
            {
                var value = StructuredTextFormatter.JsonValue(root, item.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    attributes[item.Key] = value;
                }
            }
        }

        return attributes;
    }

    private static IReadOnlyDictionary<string, string> WithSkippedCount(IReadOnlyDictionary<string, string>? existing, int skipped)
    {
        var attributes = new Dictionary<string, string>(existing ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["structuredSkippedRecords"] = skipped.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return attributes;
    }

    private static void CopySourceAttributes(IReadOnlyDictionary<string, string>? source, Dictionary<string, string> target)
    {
        if (source is null)
        {
            return;
        }

        foreach (var item in source)
        {
            if (string.Equals(item.Key, StructuredSchemaLoader.SchemaPathAttribute, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target.TryAdd(item.Key, item.Value);
        }
    }
}
