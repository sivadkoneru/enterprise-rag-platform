using System.Security.Cryptography;
using System.Text;

namespace Rag.Core.Common;

/// <summary>
/// Computes the deterministic, lowercase 24-character hex identifier used by every stable-id call
/// site (ingested document ids, parsed file document ids, structured record ids). Callers compose
/// the exact input string themselves — this only owns the hashing algorithm so it is defined once.
/// Do not change the algorithm: existing ids must remain byte-identical or previously-indexed data
/// is silently orphaned.
/// </summary>
internal static class StableId
{
    public static string Compute(string input)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..24].ToLowerInvariant();
    }
}
