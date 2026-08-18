using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Evals.Scoring;

namespace Rag.Evals.Reporting;

/// <summary>
/// Writes the committed result artifacts.
///
/// Determinism is a requirement, not a nicety: CI regenerates these files and fails the build if
/// git sees a diff, so any run-to-run variation would turn into a permanently red pipeline. Hence
/// fixed rounding, invariant formatting, explicit "\n" line endings (the repo has no .gitattributes,
/// so Environment.NewLine would produce CRLF on Windows), and no timestamps anywhere.
/// </summary>
internal static class ResultsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void WriteJson(string path, EvalRun run)
    {
        Write(path, JsonSerializer.Serialize(run, JsonOptions));
    }

    public static void Write(string path, string content)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, normalized, new UTF8Encoding(false));
    }
}
