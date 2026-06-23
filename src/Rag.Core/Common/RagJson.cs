using System.Text.Json;

namespace Rag.Core.Common;

/// <summary>
/// The default <see cref="JsonSerializerDefaults.Web"/> options shared by adapters that serialize
/// plain JSON over HTTP or to disk (Elasticsearch request/response bodies, the HTTP LLM client, and
/// the file document store). Adapters that need extra settings derive their own instance from this
/// one via the <see cref="JsonSerializerOptions(JsonSerializerOptions)"/> copy constructor rather
/// than mutating it, since a <see cref="JsonSerializerOptions"/> instance is frozen after first use.
/// </summary>
internal static class RagJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
