using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IDocumentParser
{
    string Name { get; }

    bool CanParse(string path, string? contentType = null);

    Task<ParsedDocument> ParseAsync(string path, CancellationToken cancellationToken = default);
}
