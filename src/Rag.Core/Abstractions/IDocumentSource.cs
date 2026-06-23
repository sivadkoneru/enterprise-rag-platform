using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IDocumentSource
{
    string Scheme { get; }

    bool CanRead(string sourceUri);

    IAsyncEnumerable<SourceItem> EnumerateAsync(string sourceUri, CancellationToken cancellationToken = default);
}

public interface IDocumentSourceResolver
{
    IDocumentSource Resolve(string sourceUri);
}
