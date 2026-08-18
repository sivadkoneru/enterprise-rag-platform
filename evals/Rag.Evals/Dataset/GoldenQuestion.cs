using System.Text.Json.Serialization;

namespace Rag.Evals.Dataset;

/// <summary>How a question is expected to behave, which decides how it is scored.</summary>
internal enum QuestionType
{
    /// <summary>One fact in one section.</summary>
    Lookup,

    /// <summary>Evidence spread across two or more sections; retrieval must return all of it.</summary>
    Synthesis,

    /// <summary>Deliberately not covered by the corpus.</summary>
    Unanswerable
}

internal enum Difficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>
/// A verbatim phrase from the parsed corpus that constitutes the evidence for a question.
///
/// Anchors are phrases rather than chunk identifiers because chunk ids embed both the strategy name
/// and a document id derived from an absolute filesystem path, so they differ per strategy and per
/// machine. A phrase is stable across both, and it is resolved to character offsets at load time so
/// evidence can still be compared against chunk boundaries.
/// </summary>
internal sealed record GoldAnchor(
    [property: JsonPropertyName("phrase")] string Phrase,
    [property: JsonPropertyName("section")] string Section);

internal sealed record GoldenQuestion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("type")] QuestionType Type,
    [property: JsonPropertyName("difficulty")] Difficulty Difficulty,
    [property: JsonPropertyName("expectedSourceFile")] string? ExpectedSourceFile,
    [property: JsonPropertyName("goldAnchors")] IReadOnlyList<GoldAnchor> GoldAnchors,
    [property: JsonPropertyName("answerKeywords")] IReadOnlyList<string> AnswerKeywords,
    [property: JsonPropertyName("absentTerms")] IReadOnlyList<string> AbsentTerms,
    [property: JsonPropertyName("notes")] string? Notes)
{
    public bool IsAnswerable => Type != QuestionType.Unanswerable;
}

internal sealed record GoldenDataset(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("primaryDocument")] string PrimaryDocument,
    [property: JsonPropertyName("questions")] IReadOnlyList<GoldenQuestion> Questions);

/// <summary>A question whose anchors have been located in the parsed corpus.</summary>
internal sealed record ResolvedAnchor(GoldAnchor Anchor, int Start, int End);

internal sealed record ResolvedQuestion(GoldenQuestion Question, IReadOnlyList<ResolvedAnchor> Anchors);
