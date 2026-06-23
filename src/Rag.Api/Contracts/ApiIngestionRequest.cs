using Rag.Core.Models;

namespace Rag.Api.Contracts;

public sealed record ApiIngestionRequest(string? Path = null, string? Strategy = null, IReadOnlyList<string>? Sources = null)
{
    public IReadOnlyList<string> GetSources()
    {
        return new IngestionRequest(Path, Strategy, Sources).SourceUris;
    }
}
