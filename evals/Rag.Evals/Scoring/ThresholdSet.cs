using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rag.Evals.Scoring;

/// <summary>Per-strategy regression floors that gate CI.</summary>
internal sealed record StrategyThresholds(
    [property: JsonPropertyName("recall5")] double Recall5,
    [property: JsonPropertyName("mrr5")] double Mrr5,
    [property: JsonPropertyName("citationAccuracy1")] double CitationAccuracy1,
    [property: JsonPropertyName("fullCoverage5")] double FullCoverage5,
    [property: JsonPropertyName("minChunks")] int MinChunks);

/// <summary>
/// The committed regression floors.
///
/// Calibrated from a measured run minus a small margin, so ordinary noise does not fail the build
/// but a real retrieval regression does. Lowering a floor to turn a red build green defeats the
/// point; the floors move up when a change genuinely improves retrieval.
/// </summary>
internal sealed record ThresholdSet(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("groundednessFloor")] double GroundednessFloor,
    [property: JsonPropertyName("citationIntegrityFloor")] double CitationIntegrityFloor,
    [property: JsonPropertyName("minChunksPerStrategy")] int MinChunksPerStrategy,
    [property: JsonPropertyName("strategies")] IReadOnlyDictionary<string, StrategyThresholds> Strategies)
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "thresholds.json");

    public static ThresholdSet Load(string? path = null)
    {
        var target = path ?? DefaultPath;
        if (!File.Exists(target))
        {
            throw new FileNotFoundException($"Threshold file not found at '{target}'.", target);
        }

        return JsonSerializer.Deserialize<ThresholdSet>(File.ReadAllText(target), GoldenSerializer.Options)
            ?? throw new InvalidDataException($"Threshold file at '{target}' is empty.");
    }
}

internal static class GoldenSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
