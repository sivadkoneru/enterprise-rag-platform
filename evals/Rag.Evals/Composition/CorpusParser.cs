using Rag.Core.Abstractions;
using Rag.Evals.Scoring;

namespace Rag.Evals.Composition;

/// <summary>
/// Parses corpus documents through the same <see cref="IDocumentParserResolver"/> that ingestion
/// uses, instead of selecting a parser by file extension here.
///
/// Gold anchor offsets are measured against this text and then compared against chunk boundaries
/// produced by ingestion, so both have to come from the same parser for the same file. A second
/// extension-matching implementation would agree with the resolver right up until the day the
/// resolver chose differently — a multi-document parser claiming <c>.md</c>, a content-type
/// override, a path with no extension — and from then on every anchor offset would silently stop
/// lining up with chunk boundaries, with nothing failing to say so.
/// </summary>
internal sealed class CorpusParser : IDisposable
{
    private readonly EvalHost _host;

    private CorpusParser(EvalHost host)
    {
        _host = host;
    }

    /// <summary>
    /// Parser selection depends on neither the chunking strategy nor the run profile, so the
    /// container is built with the defaults purely to obtain the registered resolver.
    /// </summary>
    public static CorpusParser Create()
    {
        return new CorpusParser(EvalHost.Build(EvalRunner.Strategies[0], RunProfile.Default([])));
    }

    public async Task<string> ParseAsync(string path, CancellationToken cancellationToken = default)
    {
        var documents = new List<string>();
        await foreach (var document in _host.Get<IDocumentParserResolver>()
            .ParseAsync(path, cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            documents.Add(document.Text);
        }

        if (documents.Count != 1)
        {
            throw new InvalidOperationException(
                $"'{path}' parsed into {documents.Count} document(s). Gold anchor offsets are offsets into " +
                "one document's text, so the eval corpus may only hold formats that yield exactly one.");
        }

        return documents[0];
    }

    public void Dispose() => _host.Dispose();
}
