using System.Net;
using System.Text.RegularExpressions;

namespace Rag.Core.Parsing;

internal static partial class HtmlTextExtractor
{
    public static string Extract(string html)
    {
        var text = ScriptOrStyle().Replace(html, " ");
        text = BlockTags().Replace(text, "\n");
        text = ListItems().Replace(text, "\n- ");
        text = TableCells().Replace(text, " ");
        text = Tags().Replace(text, " ");
        return TextNormalizer.Normalize(WebUtility.HtmlDecode(text));
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex(@"</?(address|article|aside|blockquote|br|caption|div|dl|fieldset|figcaption|figure|footer|form|h[1-6]|header|hr|main|nav|ol|p|pre|section|table|tbody|td|tfoot|th|thead|tr|ul)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTags();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItems();

    [GeneratedRegex(@"</?(td|th)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex TableCells();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
