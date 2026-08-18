using Rag.Evals;
using Rag.Evals.Composition;
using Rag.Evals.Dataset;
using Rag.Evals.Scoring;
using Xunit;

namespace Rag.Evals.Tests;

/// <summary>
/// Runs the evaluation once and shares it across every test class that needs it.
///
/// The run ingests the corpus four times and answers 50 questions against each, which is cheap
/// (hashed embeddings, no network) but not free. Sharing one run also means every assertion below
/// describes the same numbers the published tables were generated from.
/// </summary>
public sealed class EvalFixture
{
    public EvalFixture()
    {
        Run = EvalApplication.ExecuteAsync(null).GetAwaiter().GetResult();
        Thresholds = ThresholdSet.Load();
    }

    internal EvalRun Run { get; }

    internal ThresholdSet Thresholds { get; }
}

[CollectionDefinition("evaluation")]
public sealed class EvalCollection : ICollectionFixture<EvalFixture>;
