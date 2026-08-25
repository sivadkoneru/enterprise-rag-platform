namespace Rag.Evals.Dataset;

/// <summary>One ingested corpus document, indexed for phrase lookup.</summary>
internal sealed record CorpusDocument(string FileName, AnchorResolver Resolver);

/// <summary>
/// Every document the eval ingests, indexed together.
///
/// Anchors resolve against the primary document, because offsets are per-document and gold evidence
/// lives in the handbook. Absence and ambiguity, however, are properties of the whole corpus: the
/// distractors are ingested and retrievable, so a term that appears in one of them is not absent,
/// and a gold phrase duplicated into one of them is not unambiguous. Checking either against the
/// handbook alone would let exactly the drift these checks exist to catch through unnoticed.
/// </summary>
internal sealed class CorpusIndex
{
    private readonly IReadOnlyList<CorpusDocument> _documents;
    private readonly CorpusDocument _primary;

    public CorpusIndex(IReadOnlyList<CorpusDocument> documents, string primaryFile)
    {
        if (documents.Count == 0)
        {
            throw new ArgumentException("The corpus must contain at least one document.", nameof(documents));
        }

        _documents = documents;
        _primary = documents.FirstOrDefault(document =>
                string.Equals(document.FileName, primaryFile, StringComparison.Ordinal))
            ?? throw new ArgumentException(
                $"Primary document '{primaryFile}' is not part of the corpus.", nameof(primaryFile));
    }

    public string PrimaryFile => _primary.FileName;

    /// <summary>
    /// Resolves a gold phrase to a half-open range in the primary document, rejecting a phrase that
    /// also appears in another corpus document.
    /// </summary>
    public ResolvedAnchor Resolve(GoldAnchor anchor)
    {
        var (start, end) = _primary.Resolver.Resolve(anchor.Phrase);

        foreach (var document in _documents)
        {
            if (!ReferenceEquals(document, _primary) && document.Resolver.Contains(anchor.Phrase))
            {
                throw new InvalidOperationException(
                    $"Gold anchor is ambiguous: '{anchor.Phrase}' appears in '{PrimaryFile}' and also in " +
                    $"'{document.FileName}'. Extend the phrase until it identifies one document, or the " +
                    "expected evidence depends on which copy the retriever happened to return.");
            }
        }

        return new ResolvedAnchor(anchor, PrimaryFile, start, end);
    }

    /// <summary>The document containing the phrase, or null when no corpus document does.</summary>
    public string? FindDocumentContaining(string phrase)
    {
        return _documents.FirstOrDefault(document => document.Resolver.Contains(phrase))?.FileName;
    }
}
