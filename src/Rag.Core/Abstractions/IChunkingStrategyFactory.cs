namespace Rag.Core.Abstractions;

public interface IChunkingStrategyFactory
{
    IChunkingStrategy Resolve(string? name = null);

    IReadOnlyList<IChunkingStrategy> Strategies { get; }
}
