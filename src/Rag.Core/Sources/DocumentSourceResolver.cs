using Rag.Core.Abstractions;

namespace Rag.Core.Sources;

public sealed class DocumentSourceResolver(IEnumerable<IDocumentSource> sources) : IDocumentSourceResolver
{
    public IDocumentSource Resolve(string sourceUri)
    {
        var source = sources.FirstOrDefault(candidate => candidate.CanRead(sourceUri));
        if (source is null)
        {
            throw new NotSupportedException($"No document source is registered for '{sourceUri}'.");
        }

        return source;
    }
}
