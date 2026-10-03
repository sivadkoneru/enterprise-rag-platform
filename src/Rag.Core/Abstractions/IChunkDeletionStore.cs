namespace Rag.Core.Abstractions;

/// <summary>Optional maintenance capability required when replacing indexed documents.</summary>
public interface IChunkDeletionStore
{
    Task DeleteChunksAsync(string documentId, IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default);
}
