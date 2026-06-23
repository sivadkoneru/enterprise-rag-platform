using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rag.Core.Parsing;

internal static class StructuredSchemaLoader
{
    public const string SchemaPathAttribute = "schemaPath";
    public const string SourceFileNameAttribute = "sourceFileName";

    public static async Task<StructuredProfile> LoadProfileAsync(
        string path,
        string format,
        IReadOnlyDictionary<string, string>? attributes,
        CancellationToken cancellationToken)
    {
        var schemaPath = attributes is not null &&
            attributes.TryGetValue(SchemaPathAttribute, out var fromAttribute) &&
            !string.IsNullOrWhiteSpace(fromAttribute)
                ? fromAttribute
                : FindLocalSchema(path);

        if (string.IsNullOrWhiteSpace(schemaPath) || !File.Exists(schemaPath))
        {
            throw new InvalidOperationException(
                $"No structured ingestion schema found for '{path}'. Add an exact sidecar '<file>.schema.json' or directory 'rag-ingestion.schema.json'.");
        }

        await using var stream = File.OpenRead(schemaPath);
        var schema = await JsonSerializer.DeserializeAsync<StructuredSchema>(stream, StructuredSchemaJson.Options, cancellationToken)
            .ConfigureAwait(false);
        var fileName = attributes is not null &&
            attributes.TryGetValue(SourceFileNameAttribute, out var sourceFileName) &&
            !string.IsNullOrWhiteSpace(sourceFileName)
                ? sourceFileName
                : Path.GetFileName(path);
        var profile = schema?.Profiles?
            .FirstOrDefault(candidate => Matches(candidate, fileName, format));

        if (profile is null)
        {
            throw new InvalidOperationException($"Schema '{schemaPath}' does not contain a matching '{format}' profile for '{fileName}'.");
        }

        if (profile.Text is null || profile.Text.Count == 0)
        {
            throw new InvalidOperationException($"Schema '{schemaPath}' profile for '{fileName}' must include at least one text field.");
        }

        return profile;
    }

    public static string? FindLocalSchema(string path)
    {
        var exact = CandidateSchemaPaths(path).FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        var directory = Path.GetDirectoryName(path);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var schema = Path.Combine(directory, "rag-ingestion.schema.json");
            if (File.Exists(schema))
            {
                return schema;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    public static IReadOnlyList<string> CandidateSchemaNames(string sourceName)
    {
        var names = new List<string> { $"{sourceName}.schema.json" };
        if (sourceName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            names.Add($"{sourceName[..^3]}.schema.json");
        }

        names.Add(Path.Combine(Path.GetDirectoryName(sourceName) ?? string.Empty, "rag-ingestion.schema.json"));
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> CandidateSchemaPaths(string path)
    {
        yield return $"{path}.schema.json";
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{path[..^3]}.schema.json";
        }
    }

    private static bool Matches(StructuredProfile profile, string fileName, string format)
    {
        if (!FormatMatches(profile.Format, format))
        {
            return false;
        }

        if (profile.Files is null || profile.Files.Count == 0)
        {
            return true;
        }

        return profile.Files.Any(pattern => Wildcard(pattern).IsMatch(fileName));
    }

    private static bool FormatMatches(string? profileFormat, string parserFormat)
    {
        if (string.Equals(profileFormat, parserFormat, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(parserFormat, "json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(profileFormat, "jsonl", StringComparison.OrdinalIgnoreCase);
    }

    private static Regex Wildcard(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal);
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
