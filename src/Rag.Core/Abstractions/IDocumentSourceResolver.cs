namespace Rag.Core.Abstractions;

public interface IDocumentSourceResolver
{
    IDocumentSource Resolve(string sourceUri);
}
