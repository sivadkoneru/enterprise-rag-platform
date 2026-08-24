using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Evals.Scoring;

namespace Rag.Evals.Composition;

/// <summary>
/// Builds one fully isolated platform container per chunking strategy.
///
/// Every service in <c>AddRagPlatform</c> is a singleton, and the in-memory vector and document
/// stores keep their state in instance fields, so a separate container per strategy yields four
/// genuinely independent indexes without touching core registration or adding a test seam.
///
/// Configuration is built from an explicit dictionary and nothing else. The CLI and API compose
/// configuration through <c>EnvFile.LoadFromWorkingDirectory</c>, which walks up the directory tree
/// and would pick up the developer's gitignored <c>.env</c> — quietly pointing the eval at a live
/// LLM endpoint and making local numbers disagree with CI's for reasons nobody can see in the diff.
/// </summary>
internal sealed class EvalHost : IDisposable
{
    private readonly ServiceProvider _provider;

    private EvalHost(ServiceProvider provider, EmbeddingCallCounter counter)
    {
        _provider = provider;
        Counter = counter;
    }

    public EmbeddingCallCounter Counter { get; }

    public T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    public static EvalHost Build(string strategy, RunProfile profile)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["LLM_PROVIDER"] = profile.LlmProvider,
            ["DOC_STORE"] = "memory",
            ["VECTOR_STORE"] = "memory",
            ["JOB_STORE"] = "memory",
            ["CHUNKING_STRATEGY"] = strategy,
            ["CHUNK_SIZE"] = profile.ChunkSize.ToString(CultureInfo.InvariantCulture),
            ["CHUNK_OVERLAP"] = profile.ChunkOverlap.ToString(CultureInfo.InvariantCulture),

            // Set explicitly rather than left to the core default, so the published benchmark states
            // the cut policy it measured instead of tracking whatever that default happens to be.
            ["SEMANTIC_DISTANCE_THRESHOLD"] = profile.SemanticDistanceThreshold.ToString(CultureInfo.InvariantCulture),
            ["LLM_EMBEDDING_DIMENSIONS"] = profile.EmbeddingDimensions.ToString(CultureInfo.InvariantCulture),

            // Serial ingestion keeps chunk indices and document ordering identical run to run, which
            // the committed results artifact depends on.
            ["INGESTION_MAX_PARALLELISM"] = "1",
            ["LLM_SYSTEM_PROMPT"] =
                "Answer only from the supplied context. If the answer is not present, say you do not know. Always cite sources."
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddRagPlatform(configuration);

        var counter = new EmbeddingCallCounter();
        Decorate(services, counter);

        return new EvalHost(services.BuildServiceProvider(), counter);
    }

    /// <summary>
    /// Wraps whatever <c>AddRagPlatform</c> registered for <see cref="IEmbeddingClient"/> so embed
    /// calls can be counted. Done by rewriting the descriptor rather than by adding an interface to
    /// core: index cost is the harness's concern, not the platform's.
    /// </summary>
    private static void Decorate(IServiceCollection services, EmbeddingCallCounter counter)
    {
        var descriptor = services.LastOrDefault(service => service.ServiceType == typeof(IEmbeddingClient))
            ?? throw new InvalidOperationException("AddRagPlatform did not register IEmbeddingClient.");
        var factory = descriptor.ImplementationFactory
            ?? throw new InvalidOperationException("IEmbeddingClient is expected to be registered through a factory.");

        services.Remove(descriptor);
        services.AddSingleton<IEmbeddingClient>(provider =>
            new CountingEmbeddingClient((IEmbeddingClient)factory(provider), counter));
    }

    public void Dispose() => _provider.Dispose();
}

/// <summary>Mutable tally of embedding calls and characters embedded.</summary>
internal sealed class EmbeddingCallCounter
{
    public int Calls { get; private set; }

    public long Characters { get; private set; }

    public void Record(int characterCount)
    {
        Calls++;
        Characters += characterCount;
    }
}

internal sealed class CountingEmbeddingClient(IEmbeddingClient inner, EmbeddingCallCounter counter) : IEmbeddingClient
{
    public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        counter.Record(input.Length);
        return inner.EmbedAsync(input, cancellationToken);
    }
}
