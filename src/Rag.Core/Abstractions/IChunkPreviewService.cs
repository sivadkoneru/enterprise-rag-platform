using Rag.Core.Models;

namespace Rag.Core.Abstractions;

public interface IChunkPreviewService
{
    Task<IReadOnlyList<ChunkPreview>> PreviewAsync(string path, CancellationToken cancellationToken = default);
}
