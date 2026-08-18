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
        return NormalizeWithMap(input, out _);
    }

    /// <summary>
    /// Normalizes and emits <paramref name="sourceIndex"/>, where <c>sourceIndex[i]</c> is the index
    /// in <paramref name="input"/> of the character that produced normalized character <c>i</c>.
    /// That map is what lets a phrase match in normalized space and still yield exact offsets into
    /// the raw document, which is how gold evidence stays comparable to chunk boundaries.
    /// </summary>
    public static string NormalizeWithMap(string input, out int[] sourceIndex)
    {
        var builder = new StringBuilder(input.Length);
        var map = new List<int>(input.Length);
        var pendingWhitespace = false;
        var pendingWhitespaceAt = 0;

        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (char.IsWhiteSpace(character))
            {
                if (!pendingWhitespace)
                {
                    pendingWhitespace = true;
                    pendingWhitespaceAt = index;
                }

                continue;
            }

            if (pendingWhitespace)
            {
                // A whitespace run maps to its first character, so a span that starts right after a
                // line break still resolves to a sensible raw offset.
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                    map.Add(pendingWhitespaceAt);
                }

                pendingWhitespace = false;
            }

            foreach (var mapped in MapCharacter(character))
            {
                builder.Append(mapped);
                map.Add(index);
            }
        }

        sourceIndex = [.. map];
        return builder.ToString();
    }

    /// <summary>
    /// Folds typographic characters to their ASCII equivalents and lowercases. Returns a short
    /// sequence because a single source character can normalize to more than one (an ellipsis
    /// becomes three periods), and the offset map has to stay aligned when it does.
    /// </summary>
    private static ReadOnlySpan<char> MapCharacter(char character)
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
            case ' ':
                return " ";
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

    public static string Round(double value)
    {
        return Math.Round(value, 4).ToString("0.0###", CultureInfo.InvariantCulture);
    }
}
