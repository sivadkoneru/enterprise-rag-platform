using Microsoft.VisualBasic.FileIO;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Parsing;

public sealed class CsvDocumentParser : IMultiDocumentParser
{
    public string Name => "csv";

    public bool CanParse(string path, string? contentType = null)
    {
        return string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "text/csv", StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<ParsedDocument> ParseManyAsync(
        string path,
        IReadOnlyDictionary<string, string>? attributes = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var profile = await StructuredSchemaLoader.LoadProfileAsync(path, "csv", attributes, cancellationToken).ConfigureAwait(false);
        var documents = new List<ParsedDocument>();
        var skipped = 0;

        using var parser = new TextFieldParser(path)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var headers = parser.ReadFields() ?? [];
        var indexes = headers
            .Select((header, index) => (header, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.header))
            .ToDictionary(item => item.header, item => item.index, StringComparer.OrdinalIgnoreCase);
        ValidateRequiredColumns(profile, indexes, path);

        var recordIndex = 0;
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordIndex++;
            var fields = parser.ReadFields() ?? [];
            var row = headers
                .Select((header, index) => (header, Value: index < fields.Length ? fields[index] : null))
                .Where(item => !string.IsNullOrWhiteSpace(item.header))
                .ToDictionary(item => item.header, item => item.Value, StringComparer.OrdinalIgnoreCase);

            var text = StructuredTextFormatter.BuildText(
                profile.Text!.Select(field => (field, Value(row, field.Column))),
                out var missingRequired);

            if (missingRequired || string.IsNullOrWhiteSpace(text))
            {
                skipped++;
                continue;
            }

            var configuredId = Value(row, profile.Id);
            var recordKey = StructuredDocumentIds.RecordKey(configuredId, path, recordIndex, text);
            var metadata = ParserMetadata.ForFile(path, "text/csv");
            var parsedAttributes = Attributes(attributes, profile, row, recordIndex, recordKey, "csv");
            documents.Add(new ParsedDocument(recordKey, text, metadata with { DocumentId = recordKey, Attributes = parsedAttributes }));
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

    private static void ValidateRequiredColumns(StructuredProfile profile, IReadOnlyDictionary<string, int> indexes, string path)
    {
        var missing = profile.Text?
            .Where(field => field.Required && !string.IsNullOrWhiteSpace(field.Column) && !indexes.ContainsKey(field.Column))
            .Select(field => field.Column!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (missing.Length > 0)
        {
            throw new InvalidDataException($"CSV schema for '{path}' references missing required column(s): {string.Join(", ", missing)}.");
        }
    }

    private static string? Value(IReadOnlyDictionary<string, string?> row, string? column)
    {
        return !string.IsNullOrWhiteSpace(column) && row.TryGetValue(column, out var value) ? value : null;
    }

    private static IReadOnlyDictionary<string, string> Attributes(
        IReadOnlyDictionary<string, string>? sourceAttributes,
        StructuredProfile profile,
        IReadOnlyDictionary<string, string?> row,
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
                var value = Value(row, item.Value);
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
