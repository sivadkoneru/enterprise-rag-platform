using System.Text;
using System.Text.RegularExpressions;

namespace Rag.Core.Parsing;

internal static partial class TextNormalizer
{
    public static string Normalize(string input)
    {
        var normalized = input.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = MultipleSpaces().Replace(normalized, " ");
        normalized = MultipleBlankLines().Replace(normalized, "\n\n");
        return normalized.Trim();
    }

    public static string StripMarkdown(string markdown)
    {
        var withoutFences = CodeFence().Replace(markdown, match => match.Groups["code"].Value);
        var withoutLinks = Link().Replace(withoutFences, "${text}");
        var builder = new StringBuilder();
        foreach (var line in withoutLinks.Split('\n'))
        {
            builder.AppendLine(line.TrimStart('#', '>', '-', '*', '+', ' '));
        }

        return Normalize(builder.ToString());
    }

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex MultipleSpaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLines();

    [GeneratedRegex(@"```(?<code>[\s\S]*?)```")]
    private static partial Regex CodeFence();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\([^)]+\)")]
    private static partial Regex Link();
}
