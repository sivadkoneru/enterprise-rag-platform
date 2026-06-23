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

            // Rows stream out as they are read, so the skipped count is the running total at this
            // point in the file rather than the whole-file total.
            var document = StructuredRecordProjector.Project(
                profile,
                path,
                recordIndex,
                "text/csv",
                "csv",
                attributes,
                skipped,
                field => field.Column,
                column => Value(row, column));

            if (document is null)
            {
                skipped++;
                continue;
            }

            yield return document;
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

}
