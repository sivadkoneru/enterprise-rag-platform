using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IDocumentParserResolver
{
    /// <summary>
    /// Parses a document into one or more <see cref="ParsedDocument"/> values, selecting the parser
    /// that claims the path. Single-document formats yield exactly one document; structured formats
    /// (CSV, JSON, JSONL) yield one per record and stream them as they are read.
    /// </summary>
    IAsyncEnumerable<ParsedDocument> ParseAsync(
        string path,
        IReadOnlyDictionary<string, string>? attributes = null,
        string? contentType = null,
        CancellationToken cancellationToken = default);
}
