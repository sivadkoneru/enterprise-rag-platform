using Rag.Core.Common;

namespace Rag.Core.Parsing;

internal static class StructuredDocumentIds
{
    public static string RecordKey(string? configuredId, string source, int recordIndex, string text)
    {
        if (!string.IsNullOrWhiteSpace(configuredId))
        {
            return configuredId.Trim();
        }

        return StableId.Compute($"{source}|{recordIndex}|{text}");
    }
}
