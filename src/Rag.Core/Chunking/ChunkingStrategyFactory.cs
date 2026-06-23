using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;

namespace Rag.Core.Chunking;

public sealed class ChunkingStrategyFactory(IEnumerable<IChunkingStrategy> strategies, IOptions<RagOptions> options) : IChunkingStrategyFactory
{
    private readonly IReadOnlyList<IChunkingStrategy> _strategies = strategies.ToArray();

    public IReadOnlyList<IChunkingStrategy> Strategies => _strategies;

    public IChunkingStrategy Resolve(string? name = null)
    {
        var selected = Alias(string.IsNullOrWhiteSpace(name) ? options.Value.ChunkingStrategy : name);
        var strategy = _strategies.FirstOrDefault(candidate => string.Equals(candidate.Name, selected, StringComparison.OrdinalIgnoreCase));
        return strategy ?? throw new InvalidOperationException($"Chunking strategy '{selected}' is not registered.");
    }

    private static string? Alias(string? name)
    {
        return string.Equals(name, "markdown-aware", StringComparison.OrdinalIgnoreCase) ? "markdown" : name;
    }
}
