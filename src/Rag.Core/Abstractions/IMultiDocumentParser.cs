using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IMultiDocumentParser
{
    string Name { get; }

    bool CanParse(string path, string? contentType = null);

    IAsyncEnumerable<ParsedDocument> ParseManyAsync(
        string path,
        IReadOnlyDictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default);
}
