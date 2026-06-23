using Rag.Core.Models;

namespace Rag.Core.Parsing;

/// <summary>
/// Projects a single structured record (a CSV row, a JSONL line, or a JSON array item) into a
/// <see cref="ParsedDocument"/>. Shared by every structured parser so each one only has to own its
/// own format-specific reading loop and value resolution.
/// </summary>
internal static class StructuredRecordProjector
{
    /// <summary>
    /// Builds the record text, id, and metadata attributes for one record and returns the
    /// projected <see cref="ParsedDocument"/>, or <see langword="null"/> if the record has a
    /// missing required field or produces no text and should be skipped.
    /// </summary>
    /// <param name="profile">The schema profile describing the record's text fields and metadata.</param>
    /// <param name="path">Path of the file the record was read from.</param>
    /// <param name="recordIndex">1-based position of this record within the file.</param>
    /// <param name="contentType">Content type recorded on the document metadata.</param>
    /// <param name="format">Structured format name stamped on the record attributes.</param>
    /// <param name="sourceAttributes">Attributes carried from the source item, if any.</param>
    /// <param name="skippedSoFar">
    /// The caller's running skipped-record count as it stands before this record. Callers own
    /// their own counter: on a <see langword="null"/> result they increment it themselves. The
    /// count stamped on a successfully projected document is therefore the running total at the
    /// point it was yielded, not the whole-file total.
    /// </param>
    /// <param name="fieldKey">
    /// Selects which of a field's configured identifiers this format reads. Each format owns
    /// exactly one: JSON and JSONL read <see cref="StructuredField.Path"/>, CSV reads
    /// <see cref="StructuredField.Column"/>. Keeping the choice explicit means a schema that
    /// happens to set both still resolves through its own format's identifier.
    /// </param>
    /// <param name="resolveValue">
    /// Resolves a field's configured pointer/column identifier (a JSON pointer or a CSV column
    /// name) to its raw value for the current record.
    /// </param>
    public static ParsedDocument? Project(
        StructuredProfile profile,
        string path,
        int recordIndex,
        string contentType,
        string format,
        IReadOnlyDictionary<string, string>? sourceAttributes,
        int skippedSoFar,
        Func<StructuredField, string?> fieldKey,
        Func<string?, string?> resolveValue)
    {
        var text = StructuredTextFormatter.BuildText(
            profile.Text!.Select(field => (field, resolveValue(fieldKey(field)))),
            out var missingRequired);

        if (missingRequired || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var configuredId = resolveValue(profile.Id);
        var recordKey = StructuredDocumentIds.RecordKey(configuredId, path, recordIndex, text);
        var metadata = ParserMetadata.ForFile(path, contentType);
        var parsedAttributes = StructuredAttributes.ForRecord(
            sourceAttributes,
            profile,
            recordIndex,
            recordKey,
            format,
            resolveValue);

        return new ParsedDocument(
            recordKey,
            text,
            metadata with
            {
                DocumentId = recordKey,
                Attributes = StructuredAttributes.WithSkippedCount(parsedAttributes, skippedSoFar)
            });
    }
}
