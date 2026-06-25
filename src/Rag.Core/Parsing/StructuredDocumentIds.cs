using System.Security.Cryptography;
using System.Text;

namespace Rag.Core.Parsing;

internal static class StructuredDocumentIds
{
    public static string RecordKey(string? configuredId, string source, int recordIndex, string text)
    {
        if (!string.IsNullOrWhiteSpace(configuredId))
        {
            return configuredId.Trim();
        }

        var input = $"{source}|{recordIndex}|{text}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..24].ToLowerInvariant();
    }
}
