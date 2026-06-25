using System.Globalization;

namespace Rag.Core.Parsing;

/// <summary>
/// Builds the per-record metadata attributes shared by every structured (CSV/JSON/JSONL) parser.
/// </summary>
internal static class StructuredAttributes
{
    public const string RecordIndexKey = "recordIndex";
    public const string RecordKeyKey = "recordKey";
    public const string FormatKey = "structuredFormat";
    public const string SkippedRecordsKey = "structuredSkippedRecords";

    /// <summary>
    /// Combines the built-in record attributes, the source item attributes, and the schema's
    /// configured metadata projections. <paramref name="metadataValue"/> resolves a schema
    /// projection (a JSON pointer or a CSV column) against the current record.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ForRecord(
        IReadOnlyDictionary<string, string>? sourceAttributes,
        StructuredProfile profile,
        int recordIndex,
        string recordKey,
        string format,
        Func<string?, string?> metadataValue)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RecordIndexKey] = recordIndex.ToString(CultureInfo.InvariantCulture),
            [RecordKeyKey] = recordKey,
            [FormatKey] = format
        };

        if (sourceAttributes is not null)
        {
            foreach (var attribute in sourceAttributes)
            {
                // The schema sidecar path is an ingestion detail, not document metadata.
                if (!string.Equals(attribute.Key, StructuredSchemaLoader.SchemaPathAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    attributes.TryAdd(attribute.Key, attribute.Value);
                }
            }
        }

        if (profile.Metadata is not null)
        {
            foreach (var projection in profile.Metadata)
            {
                var value = metadataValue(projection.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    attributes[projection.Key] = value;
                }
            }
        }

        return attributes;
    }

    public static IReadOnlyDictionary<string, string> WithSkippedCount(IReadOnlyDictionary<string, string>? existing, int skipped)
    {
        return new Dictionary<string, string>(existing ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            [SkippedRecordsKey] = skipped.ToString(CultureInfo.InvariantCulture)
        };
    }
}
