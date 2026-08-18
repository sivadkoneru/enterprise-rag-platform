using Rag.Evals.Scoring;

namespace Rag.Evals.Dataset;

/// <summary>
/// Locates gold phrases inside a parsed document and converts them to character ranges.
///
/// Resolution is strict on purpose. A phrase that no longer appears, or that appears more than
/// once, is a broken label rather than a hard question: the first would silently score as an
/// unretrievable question forever, and the second would make "the right chunk" ambiguous. Both
/// throw, which turns corpus drift into a failing test naming the phrase instead of a quietly
/// wrong benchmark.
/// </summary>
internal sealed class AnchorResolver
{
    private readonly string _normalized;
    private readonly int[] _sourceIndex;
    private readonly int _rawLength;

    public AnchorResolver(string documentText)
    {
        _normalized = TextNormalization.NormalizeWithMap(documentText, out _sourceIndex);
        _rawLength = documentText.Length;
    }

    public string NormalizedText => _normalized;

    /// <summary>Resolves a phrase to a half-open <c>[start, end)</c> range in the raw document text.</summary>
    public (int Start, int End) Resolve(string phrase)
    {
        var needle = TextNormalization.Normalize(phrase);
        if (needle.Length == 0)
        {
            throw new InvalidOperationException("A gold anchor phrase cannot be empty.");
        }

        var first = _normalized.IndexOf(needle, StringComparison.Ordinal);
        if (first < 0)
        {
            throw new InvalidOperationException(
                $"Gold anchor not found in the corpus: '{phrase}'. Re-author it from `dump-text` output; " +
                "the corpus text is authoritative, not the generator source.");
        }

        if (_normalized.IndexOf(needle, first + 1, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"Gold anchor is ambiguous: '{phrase}' appears more than once in the corpus. " +
                "Extend the phrase until it is unique so the expected evidence is unambiguous.");
        }

        var start = _sourceIndex[first];
        var lastNormalized = first + needle.Length - 1;
        var end = _sourceIndex[lastNormalized] + 1;
        return (start, Math.Min(end, _rawLength));
    }

    /// <summary>True when the phrase does not occur at all; used to prove unanswerable questions are unanswerable.</summary>
    public bool Contains(string phrase)
    {
        var needle = TextNormalization.Normalize(phrase);
        return needle.Length > 0 && _normalized.Contains(needle, StringComparison.Ordinal);
    }
}
