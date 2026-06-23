using Rag.Core.Models;

namespace Rag.Core.Chunking;

internal static class ChunkBuilder
{
    public static TextChunk Create(ParsedDocument document, string strategy, int index, int start, int end)
    {
        var safeStart = Math.Max(0, Math.Min(start, document.Text.Length));
        var safeEnd = Math.Max(safeStart, Math.Min(end, document.Text.Length));
        var text = document.Text[safeStart..safeEnd].Trim();
        var id = $"{document.Id}:{strategy}:{index:D5}";
        return new TextChunk(id, document.Id, index, text, safeStart, safeEnd, document.Metadata);
    }
}
