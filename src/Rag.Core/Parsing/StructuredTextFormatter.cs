using System.Text;
using System.Text.Json;

namespace Rag.Core.Parsing;

internal static class StructuredTextFormatter
{
    public static string BuildText(IEnumerable<(StructuredField Field, string? Value)> values, out bool missingRequired)
    {
        missingRequired = false;
        var builder = new StringBuilder();

        foreach (var (field, rawValue) in values)
        {
            var value = Format(rawValue, field.Format);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.Required)
                {
                    missingRequired = true;
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(field.Label))
            {
                builder.AppendLine(field.Label);
            }

            builder.AppendLine(value);
            builder.AppendLine();
        }

        return TextNormalizer.Normalize(builder.ToString());
    }

    public static string? JsonValue(JsonElement element, string? pointer)
    {
        if (string.IsNullOrWhiteSpace(pointer))
        {
            return null;
        }

        if (!TryResolvePointer(element, pointer, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText()
        };
    }

    public static string Format(string? value, string? format)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? "plain" : format.Trim().ToLowerInvariant();
        if (normalizedFormat == "auto")
        {
            normalizedFormat = LooksLikeHtml(value) ? "html" : LooksLikeMarkdown(value) ? "markdown" : "plain";
        }

        return normalizedFormat switch
        {
            "html" => HtmlTextExtractor.Extract(value),
            "markdown" => TextNormalizer.StripMarkdown(value),
            _ => TextNormalizer.Normalize(value)
        };
    }

    private static bool TryResolvePointer(JsonElement root, string pointer, out JsonElement value)
    {
        value = root;
        if (pointer == "/")
        {
            return true;
        }

        if (!pointer.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in pointer.Split('/').Skip(1).Select(UnescapePointerSegment))
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(segment, out value))
                {
                    return false;
                }
            }
            else if (value.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index) && index >= 0 && index < value.GetArrayLength())
            {
                value = value[index];
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static string UnescapePointerSegment(string segment)
    {
        return segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
    }

    private static bool LooksLikeHtml(string value)
    {
        return value.Contains('<', StringComparison.Ordinal) && value.Contains('>', StringComparison.Ordinal);
    }

    private static bool LooksLikeMarkdown(string value)
    {
        return value.Contains("# ", StringComparison.Ordinal) ||
            value.Contains("](", StringComparison.Ordinal) ||
            value.Contains("```", StringComparison.Ordinal);
    }
}
