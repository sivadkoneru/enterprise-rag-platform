using Rag.Core.Llm;
using Rag.Core.Models;
using Rag.Evals.Dataset;

namespace Rag.Evals.Scoring;

/// <summary>
/// The metric definitions, kept in one place so the README can point at a single file for "how is
/// this actually computed".
/// </summary>
internal static class Scorers
{
    /// <summary>Rounding applied to every published number, so the committed artifacts stay stable.</summary>
    public static double Round(double value) => Math.Round(value, 4);

    /// <summary>
    /// True when a single chunk from the anchor's own document contains the evidence phrase
    /// outright.
    ///
    /// This is the authoritative notion of "the model saw the evidence", because a chunk is what
    /// gets pasted into the prompt as one block. The document check matters because the corpus
    /// contains distractors that deliberately restate handbook policy in different terms: a phrase
    /// match in one of those is a retrieval mistake, not evidence.
    /// </summary>
    public static bool Covers(TextChunk chunk, ResolvedAnchor anchor)
    {
        return IsFromAnchorDocument(chunk, anchor)
            && TextNormalization.Normalize(chunk.Text)
                .Contains(TextNormalization.Normalize(anchor.Anchor.Phrase), StringComparison.Ordinal);
    }

    /// <summary>
    /// True when the retrieved set jointly spans the evidence even though no single chunk contains
    /// it. Fixed-size chunking splits mid-sentence, so a 60-character phrase routinely straddles a
    /// boundary; without this the metric would punish a retriever that actually returned everything
    /// needed. Tracked separately as the fragmentation rate, since evidence split across two blocks
    /// is genuinely worse than evidence delivered whole.
    ///
    /// Only chunks from the anchor's own document take part. Offsets are offsets into one
    /// document's text, so merging a distractor's [0, 800) with a handbook anchor's range would
    /// "cover" evidence that was never retrieved and silently inflate recall.
    /// </summary>
    public static bool UnionCovers(IReadOnlyList<TextChunk> chunks, ResolvedAnchor anchor)
    {
        var intervals = chunks
            .Where(chunk => IsFromAnchorDocument(chunk, anchor))
            .Select(chunk => (Start: chunk.StartOffset, End: chunk.EndOffset))
            .OrderBy(interval => interval.Start)
            .ToList();

        var cursorStart = -1;
        var cursorEnd = -1;
        foreach (var (start, end) in intervals)
        {
            if (cursorEnd >= start && cursorStart >= 0)
            {
                cursorEnd = Math.Max(cursorEnd, end);
            }
            else
            {
                if (cursorStart >= 0 && cursorStart <= anchor.Start && cursorEnd >= anchor.End)
                {
                    return true;
                }

                cursorStart = start;
                cursorEnd = end;
            }
        }

        return cursorStart >= 0 && cursorStart <= anchor.Start && cursorEnd >= anchor.End;
    }

    public static bool Found(IReadOnlyList<TextChunk> chunks, ResolvedAnchor anchor)
    {
        return chunks.Any(chunk => Covers(chunk, anchor)) || UnionCovers(chunks, anchor);
    }

    private static bool IsFromAnchorDocument(TextChunk chunk, ResolvedAnchor anchor)
    {
        return string.Equals(chunk.Metadata.FileName, anchor.SourceFile, StringComparison.Ordinal);
    }

    /// <summary>1-based rank of the first chunk that contains any anchor outright; 0 when none does.</summary>
    public static int FirstRelevantRank(IReadOnlyList<TextChunk> chunks, IReadOnlyList<ResolvedAnchor> anchors)
    {
        for (var index = 0; index < chunks.Count; index++)
        {
            if (anchors.Any(anchor => Covers(chunks[index], anchor)))
            {
                return index + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// Share of the answer's content tokens that appear in the retrieved context.
    ///
    /// The deterministic chat client prefixes every answer with a fixed phrase; leaving it in would
    /// let the client's own boilerplate inflate its score, so it is stripped before tokenising.
    /// Under that client the result sits at 1.0 by construction, because the answer is copied from
    /// the context. That makes this a regression tripwire rather than a discriminator: it fires the
    /// moment the pipeline sends a prompt that disagrees with the citations it returns, and it
    /// becomes a real measurement as soon as a live model is configured.
    /// </summary>
    public static (double Score, IReadOnlyList<string> Ungrounded) Groundedness(
        string answer,
        IReadOnlyList<TextChunk> chunks)
    {
        var stripped = answer.StartsWith(DeterministicLlmClient.AnswerPrefix, StringComparison.Ordinal)
            ? answer[DeterministicLlmClient.AnswerPrefix.Length..]
            : answer;

        var answerTokens = TextNormalization.Tokenize(stripped);
        if (answerTokens.Count == 0)
        {
            return (1, []);
        }

        var contextTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in chunks)
        {
            contextTokens.UnionWith(TextNormalization.Tokenize(chunk.Text));
        }

        var ungrounded = answerTokens.Where(token => !contextTokens.Contains(token)).Distinct(StringComparer.Ordinal).ToArray();
        var grounded = answerTokens.Count(contextTokens.Contains);
        return ((double)grounded / answerTokens.Count, ungrounded);
    }

    public static bool AnswerKeywordsHit(string answer, IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        var normalized = TextNormalization.Normalize(answer);
        return keywords.All(keyword =>
            normalized.Contains(TextNormalization.Normalize(keyword), StringComparison.Ordinal));
    }

    /// <summary>
    /// How far the best match stands out from the field, in standard deviations.
    ///
    /// Raw cosine scores cannot be compared across strategies: a bag-of-words vector dilutes as the
    /// chunk grows, so a strategy that emits large chunks scores uniformly lower without retrieving
    /// any worse. A z-score is scale-free and answers the question that actually matters for
    /// abstention: does anything in the index stand out for this query, or is the top hit just the
    /// least-bad of a uniform field?
    ///
    /// This is a harness policy, not product behavior. The platform does not abstain today, and
    /// nothing in the query pipeline consults this number.
    /// </summary>
    public static double SupportZScore(IReadOnlyList<double> scores)
    {
        if (scores.Count < 2)
        {
            return 0;
        }

        var mean = scores.Average();
        var variance = scores.Sum(score => (score - mean) * (score - mean)) / scores.Count;
        var deviation = Math.Sqrt(variance);
        return deviation <= double.Epsilon ? 0 : (scores.Max() - mean) / deviation;
    }

    public static int Percentile(IReadOnlyList<int> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        var index = (int)Math.Ceiling(percentile / 100.0 * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    /// <summary>Rough token estimate. One documented rule, applied everywhere, so figures stay comparable.</summary>
    public static int EstimateTokens(int characterCount)
    {
        return characterCount <= 0 ? 0 : (int)Math.Ceiling(characterCount / 4.0);
    }
}
