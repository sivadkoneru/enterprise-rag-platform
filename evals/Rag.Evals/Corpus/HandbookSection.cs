namespace Rag.Evals.Corpus;

/// <summary>One numbered handbook subsection: a heading plus its body paragraphs.</summary>
internal sealed record HandbookSection(string Heading, IReadOnlyList<string> Paragraphs);
