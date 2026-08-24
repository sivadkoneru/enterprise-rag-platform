using FluentAssertions;
using Rag.Evals;
using Rag.Evals.Dataset;
using Xunit;

namespace Rag.Evals.Tests;

/// <summary>
/// Guards the golden dataset against corpus drift.
///
/// Every failure mode here is silent if unchecked: an anchor that stops resolving scores as a
/// permanently missed question, an ambiguous anchor makes "the right chunk" undefined, and an
/// unanswerable question whose subject was later added to the handbook starts punishing correct
/// behavior. Each becomes a named failure instead.
/// </summary>
public sealed class DatasetIntegrityTests
{
    private static async Task<IReadOnlyList<ResolvedQuestion>> ResolveAsync()
    {
        // Resolved against every ingested document, not the handbook alone: the distractors are
        // retrievable, so a term that appears in one of them is not absent and a gold phrase
        // duplicated into one of them is not unambiguous.
        var dataset = GoldenDatasetLoader.Load();
        return GoldenDatasetLoader.Resolve(dataset, await EvalApplication.BuildCorpusIndexAsync());
    }

    [Fact]
    public async Task EveryGoldAnchorResolvesToExactlyOneSpanInTheCorpus()
    {
        var resolved = await ResolveAsync();

        resolved.Should().HaveCount(50);
        resolved.SelectMany(item => item.Anchors).Should().NotBeEmpty();
        resolved.SelectMany(item => item.Anchors)
            .Should().OnlyContain(anchor => anchor.End > anchor.Start, "each anchor must span real text");
    }

    [Fact]
    public async Task DatasetCoversLookupSynthesisAndUnanswerableQuestions()
    {
        var resolved = await ResolveAsync();

        // A dataset of only single-fact lookups would score every chunking strategy the same. The
        // mix is what makes the benchmark able to separate them.
        resolved.Count(item => item.Question.Type == QuestionType.Lookup).Should().Be(30);
        resolved.Count(item => item.Question.Type == QuestionType.Synthesis).Should().Be(12);
        resolved.Count(item => item.Question.Type == QuestionType.Unanswerable).Should().Be(8);
    }

    [Fact]
    public async Task SynthesisQuestionsRequireEvidenceFromMoreThanOneSection()
    {
        var resolved = await ResolveAsync();

        foreach (var item in resolved.Where(item => item.Question.Type == QuestionType.Synthesis))
        {
            item.Anchors.Select(anchor => anchor.Anchor.Section)
                .Distinct(StringComparer.Ordinal)
                .Should().HaveCountGreaterThanOrEqualTo(2, "synthesis question {0} must span sections", item.Question.Id);
        }
    }

    [Fact]
    public async Task DeclaredDifficultyMatchesMeasuredLexicalOverlap()
    {
        var resolved = await ResolveAsync();

        foreach (var item in resolved.Where(item => item.Question.IsAnswerable))
        {
            var overlap = GoldenDatasetLoader.LexicalOverlap(item);
            GoldenDatasetLoader.BandFor(overlap)
                .Should().Be(item.Question.Difficulty,
                    "question {0} declares a difficulty that its measured overlap of {1:F3} does not support",
                    item.Question.Id,
                    overlap);
        }
    }

    [Fact]
    public async Task EveryDifficultyBandIsPopulated()
    {
        var resolved = await ResolveAsync();

        // A benchmark whose questions are all easy cannot show a difference between strategies.
        var bands = resolved.Where(item => item.Question.IsAnswerable)
            .GroupBy(item => item.Question.Difficulty)
            .ToDictionary(group => group.Key, group => group.Count());

        bands.Should().ContainKeys(Difficulty.Easy, Difficulty.Medium, Difficulty.Hard);
        bands.Values.Should().OnlyContain(count => count >= 5);
    }

    [Fact]
    public async Task UnanswerableQuestionsNameTermsThatAreAbsentFromTheCorpus()
    {
        var resolved = await ResolveAsync();

        foreach (var item in resolved.Where(item => !item.Question.IsAnswerable))
        {
            item.Question.AbsentTerms.Should().NotBeEmpty(
                "unanswerable question {0} must name the terms that prove it is unanswerable", item.Question.Id);
        }
    }
}
