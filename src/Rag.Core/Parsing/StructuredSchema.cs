using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Core.Common;

namespace Rag.Core.Parsing;

internal sealed record StructuredSchema(
    int Version = 1,
    IReadOnlyList<StructuredProfile>? Profiles = null);

internal sealed record StructuredProfile(
    IReadOnlyList<string>? Files = null,
    string? Format = null,
    string? Id = null,
    IReadOnlyList<StructuredField>? Text = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

internal static class StructuredSchemaJson
{
    public static readonly JsonSerializerOptions Options = new(RagJson.Options)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
