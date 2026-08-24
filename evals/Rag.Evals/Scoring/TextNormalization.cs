using System.Globalization;
using System.Text;

namespace Rag.Evals.Scoring;

/// <summary>
/// Shared text canonicalisation for matching and tokenising.
///
/// Matching has to be insensitive to how the corpus was laid out. PDF extraction breaks sentences
/// across lines, so a gold phrase authored as flat prose contains no newline while the document
/// does. Collapsing every whitespace run to a single space makes the two comparable while keeping
/// punctuation, so "thirty days." stays distinguishable from "thirty days of".
/// </summary>
internal static class TextNormalization
{
    /// <summary>
    /// Tokens carrying no retrieval signal. Kept deliberately short: an aggressive stopword list
    /// would inflate groundedness by discarding exactly the words a hallucination introduces.
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "but", "if", "of", "to", "in", "on", "at", "by", "for",
        "with", "from", "as", "is", "are", "was", "were", "be", "been", "being", "it", "its",
        "this", "that", "these", "those", "there", "their", "they", "them", "he", "she", "his",
        "her", "we", "our", "you", "your", "i", "do", "does", "did", "has", "have", "had", "not",
        "no", "so", "than", "then", "when", "which", "who", "whom", "what", "how", "why", "can",
        "may", "must", "will", "would", "should", "could", "any", "all", "each", "other", "into",
        "up", "out", "over", "under", "about", "before", "after", "during", "while", "because"
    };

    /// <summary>
    /// Canonical form used for every substring comparison: NFKC, ASCII punctuation, single spaces,
    /// lowercase. Punctuation is preserved.
    /// </summary>
    public static string Normalize(string input)
    {
        return NormalizeWithMap(input, out _, out _);
    }

    /// <summary>
    /// Normalizes and emits the offset maps that let a phrase match in normalized space and still
    /// yield exact offsets into the raw document, which is how gold evidence stays comparable to
    /// chunk boundaries. <paramref name="sourceStart"/><c>[i]</c> is the first index in
    /// <paramref name="input"/> of the source span that produced normalized character <c>i</c>, and
    /// <paramref name="sourceEnd"/><c>[i]</c> is one past its last. The two differ by more than one
    /// only where NFKC contracted a span — a base letter plus a combining acute becoming a single
    /// precomposed character — and taking the end from the start map there would report a range one
    /// character short of the text that actually matched.
    /// </summary>
    public static string NormalizeWithMap(string input, out int[] sourceStart, out int[] sourceEnd)
    {
        var builder = new StringBuilder(input.Length);
        var starts = new List<int>(input.Length);
        var ends = new List<int>(input.Length);
        var pendingWhitespace = false;
        var pendingWhitespaceAt = 0;

        var index = 0;
        while (index < input.Length)
        {
            var character = input[index];
            if (char.IsWhiteSpace(character))
            {
                if (!pendingWhitespace)
                {
                    pendingWhitespace = true;
                    pendingWhitespaceAt = index;
                }

                index++;
                continue;
            }

            if (pendingWhitespace)
            {
                // A whitespace run maps to its first character, so a span that starts right after a
                // line break still resolves to a sensible raw offset.
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                    starts.Add(pendingWhitespaceAt);
                    ends.Add(pendingWhitespaceAt + 1);
                }

                pendingWhitespace = false;
            }

            // Fast path: an ASCII character with no combining mark after it is already in NFKC, so
            // the whole corpus skips the per-cluster allocation below.
            if (char.IsAscii(character) && !FollowedByCombiningMark(input, index))
            {
                Append(builder, starts, ends, Fold(character), index, index + 1);
                index++;
                continue;
            }

            var length = ClusterLength(input, index);
            foreach (var composed in Compose(input.AsSpan(index, length)))
            {
                Append(builder, starts, ends, Fold(composed), index, index + length);
            }

            index += length;
        }

        sourceStart = [.. starts];
        sourceEnd = [.. ends];
        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder,
        List<int> starts,
        List<int> ends,
        ReadOnlySpan<char> mapped,
        int start,
        int end)
    {
        foreach (var character in mapped)
        {
            builder.Append(character);
            starts.Add(start);
            ends.Add(end);
        }
    }

    /// <summary>
    /// Applies NFKC to one base character plus the combining marks attached to it.
    ///
    /// Normalization runs per cluster rather than over the whole string because the offset map has
    /// to stay aligned with the raw text: NFKC both expands (an ellipsis becomes three periods) and
    /// contracts (a letter plus a combining acute becomes one precomposed character), and every
    /// character it produces is attributed to the cluster that produced it. Without this a gold
    /// phrase authored with a precomposed "é" would fail to match a PDF that extracted the
    /// decomposed form, and the failure would surface as "gold anchor not found" — corpus drift
    /// rather than the normalization gap it actually is.
    /// </summary>
    private static string Compose(ReadOnlySpan<char> cluster)
    {
        var text = new string(cluster);

        // string.Normalize throws on an unpaired surrogate, which is not text and cannot be folded.
        return IsWellFormed(text) ? text.Normalize(NormalizationForm.FormKC) : text;
    }

    private static bool IsWellFormed(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsSurrogate(text[index]))
            {
                continue;
            }

            if (!char.IsHighSurrogate(text[index]) || index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    /// <summary>Length in chars of the base character at <paramref name="start"/> plus its combining marks.</summary>
    private static int ClusterLength(string input, int start)
    {
        var length = char.IsHighSurrogate(input[start]) && start + 1 < input.Length && char.IsLowSurrogate(input[start + 1])
            ? 2
            : 1;

        while (start + length < input.Length && IsCombiningMark(input[start + length]))
        {
            length++;
        }

        return length;
    }

    private static bool FollowedByCombiningMark(string input, int index)
    {
        return index + 1 < input.Length && IsCombiningMark(input[index + 1]);
    }

    private static bool IsCombiningMark(char character)
    {
        return CharUnicodeInfo.GetUnicodeCategory(character)
            is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }

    /// <summary>
    /// Lowercases and folds the typographic punctuation NFKC leaves alone — curly quotes and the
    /// dash family are distinct characters, not compatibility variants, so a phrase typed with an
    /// ASCII apostrophe would otherwise never match a document that renders a right single quote.
    /// Returns a sequence because one source character can fold to more than one.
    /// </summary>
    private static ReadOnlySpan<char> Fold(char character)
    {
        switch (character)
        {
            case '‘':
            case '’':
            case '‛':
            case 'ʼ':
                return "'";
            case '“':
            case '”':
            case '‟':
                return "\"";
            case '‐':
            case '‑':
            case '‒':
            case '–':
            case '—':
            case '―':
            case '−':
                return "-";
            case '…':
                return "...";
            default:
                break;
        }

        var lowered = char.ToLowerInvariant(character);
        return new string(lowered, 1);
    }

    /// <summary>Content tokens used by the lexical metrics: alphanumeric, lowercase, stopwords removed.</summary>
    public static IReadOnlyList<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var builder = new StringBuilder();

        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            AddToken(tokens, builder);
        }

        AddToken(tokens, builder);
        return tokens;
    }

    public static HashSet<string> TokenSet(string input)
    {
        return [.. Tokenize(input)];
    }

    private static void AddToken(List<string> tokens, StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return;
        }

        var token = builder.ToString();
        builder.Clear();
        if (token.Length >= 2 && !Stopwords.Contains(token))
        {
            tokens.Add(token);
        }
    }
}
