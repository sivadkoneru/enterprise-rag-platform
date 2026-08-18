using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Evals.Scoring;

namespace Rag.Evals.Dataset;

/// <summary>
/// Reads <c>Data/golden.json</c> and binds it to the parsed corpus.
///
/// Loading is where the dataset proves it still describes reality: every anchor must resolve to
/// exactly one span, and every <c>absentTerms</c> entry must genuinely be absent. Both checks exist
/// because the failure they catch is silent — an unresolvable anchor scores as a permanently missed
/// question, and an "unanswerable" question whose subject was later added to the handbook quietly
/// starts punishing correct behavior.
/// </summary>
internal static class GoldenDatasetLoader
{
    public static JsonSerializerOptions SerializerOptions => GoldenSerializer.Options;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "golden.json");

    public static GoldenDataset Load(string? path = null)
    {
        var target = path ?? DefaultPath;
        if (!File.Exists(target))
        {
            throw new FileNotFoundException($"Golden dataset not found at '{target}'.", target);
        }

        return JsonSerializer.Deserialize<GoldenDataset>(File.ReadAllText(target), SerializerOptions)
            ?? throw new InvalidDataException($"Golden dataset at '{target}' is empty.");
    }

    /// <summary>Resolves every anchor against the corpus, throwing on the first broken label.</summary>
    public static IReadOnlyList<ResolvedQuestion> Resolve(GoldenDataset dataset, AnchorResolver resolver)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new List<ResolvedQuestion>(dataset.Questions.Count);

        foreach (var question in dataset.Questions)
        {
            if (!seenIds.Add(question.Id))
            {
                throw new InvalidDataException($"Duplicate question id '{question.Id}'.");
            }

            if (question.IsAnswerable && question.GoldAnchors.Count == 0)
            {
                throw new InvalidDataException($"Question '{question.Id}' is answerable but has no gold anchors.");
            }

            if (!question.IsAnswerable && question.GoldAnchors.Count > 0)
            {
                throw new InvalidDataException($"Question '{question.Id}' is unanswerable but declares gold anchors.");
            }

            foreach (var term in question.AbsentTerms)
            {
                if (resolver.Contains(term))
                {
                    throw new InvalidDataException(
                        $"Question '{question.Id}' is marked unanswerable, but the corpus now contains '{term}'. " +
                        "Either the handbook gained a topic it should not have, or the question is no longer unanswerable.");
                }
            }

            var anchors = new List<ResolvedAnchor>(question.GoldAnchors.Count);
            foreach (var anchor in question.GoldAnchors)
            {
                try
                {
                    var (start, end) = resolver.Resolve(anchor.Phrase);
                    anchors.Add(new ResolvedAnchor(anchor, start, end));
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidDataException($"Question '{question.Id}': {exception.Message}", exception);
                }
            }

            if (question.Type == QuestionType.Synthesis &&
                anchors.Select(anchor => anchor.Anchor.Section).Distinct(StringComparer.Ordinal).Count() < 2)
            {
                throw new InvalidDataException(
                    $"Question '{question.Id}' is a synthesis question but all its anchors sit in one section.");
            }

            resolved.Add(new ResolvedQuestion(question, anchors));
        }

        return resolved;
    }

    /// <summary>
    /// Fraction of the question's content tokens that also appear in its evidence phrase.
    ///
    /// Difficulty is measured rather than assigned by feel, because the default embedding client is
    /// a hashed bag of words with no stemming: "vendors" and "vendor" land in different buckets, so
    /// how much exact vocabulary a question shares with its evidence really does predict how hard
    /// retrieval is.
    ///
    /// A zero score is not a broken label. Retrieval scores whole chunks, not bare anchor phrases,
    /// so a question sharing no token with its evidence sentence can still be found through the
    /// surrounding prose. Those questions are the most interesting cell in the benchmark: whether
    /// the surrounding context survives into the same chunk is precisely what the chunking
    /// strategies disagree about.
    /// </summary>
    public static double LexicalOverlap(ResolvedQuestion resolved)
    {
        var questionTokens = TextNormalization.TokenSet(resolved.Question.Question);
        if (questionTokens.Count == 0)
        {
            return 0;
        }

        var anchorTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var anchor in resolved.Anchors)
        {
            anchorTokens.UnionWith(TextNormalization.TokenSet(anchor.Anchor.Phrase));
        }

        return (double)questionTokens.Count(anchorTokens.Contains) / questionTokens.Count;
    }

    public static Difficulty BandFor(double overlap)
    {
        return overlap >= 0.60 ? Difficulty.Easy
            : overlap >= 0.30 ? Difficulty.Medium
            : Difficulty.Hard;
    }
}
