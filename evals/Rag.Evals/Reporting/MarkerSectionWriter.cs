using System.Text;

namespace Rag.Evals.Reporting;

/// <summary>
/// Replaces the content between two HTML comment markers in a file, leaving everything else alone.
///
/// Generated tables are injected rather than regenerating whole documents so that hand-written prose
/// around them survives. A missing marker pair throws instead of appending: silently adding a second
/// copy of a table to the README is worse than failing the step that generates it.
/// </summary>
internal static class MarkerSectionWriter
{
    public static string Replace(string document, string marker, string content)
    {
        var begin = $"<!-- BEGIN:{marker} -->";
        var end = $"<!-- END:{marker} -->";

        var beginIndex = document.IndexOf(begin, StringComparison.Ordinal);
        var endIndex = document.IndexOf(end, StringComparison.Ordinal);

        if (beginIndex < 0 || endIndex < 0 || endIndex < beginIndex)
        {
            throw new InvalidOperationException(
                $"Could not find the '{begin}' / '{end}' marker pair. Add both markers where the " +
                "generated table belongs before running the report writer.");
        }

        var builder = new StringBuilder();
        builder.Append(document, 0, beginIndex + begin.Length);
        builder.Append('\n');
        builder.Append(content.TrimEnd('\n'));
        builder.Append('\n');
        builder.Append(document, endIndex, document.Length - endIndex);
        return builder.ToString();
    }
}
