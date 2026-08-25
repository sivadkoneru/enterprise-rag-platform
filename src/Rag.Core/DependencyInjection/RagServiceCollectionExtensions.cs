using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Chunking;
using Rag.Core.Configuration;
using Rag.Core.Jobs;
using Rag.Core.Llm;
using Rag.Core.Parsing;
using Rag.Core.Pipelines;
using Rag.Core.Sources;
using Rag.Core.Stores;
using Rag.Core.Vector;

namespace Rag.Core.DependencyInjection;

public static class RagServiceCollectionExtensions
{
    public static IServiceCollection AddRagPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        // Registered explicitly rather than relied on transitively: AddHttpClient happens to call
        // AddLogging today, but every adapter here takes a required ILogger<T>, so the dependency
        // belongs in this method rather than in another registration's implementation detail.
        services.AddLogging();

        services.Configure<RagOptions>(options =>
        {
            configuration.GetSection("Rag").Bind(options);
            options.ChunkingStrategy = configuration["CHUNKING_STRATEGY"] ?? options.ChunkingStrategy;
        });
        services.Configure<ChunkingOptions>(options =>
        {
            configuration.GetSection("Chunking").Bind(options);
            options.Size = Int(configuration["CHUNK_SIZE"], options.Size);
            options.Overlap = Int(configuration["CHUNK_OVERLAP"], options.Overlap);

            // Only the distance-named variable is honoured. An alias named "...SIMILARITY_THRESHOLD"
            // used to bind here too, which meant a value chosen as a similarity (0.78) was applied
            // as a distance and inverted the cut policy for every host that set it.
            options.SemanticDistanceThreshold = Double(
                configuration["SEMANTIC_DISTANCE_THRESHOLD"],
                options.SemanticDistanceThreshold);
        });
        services.Configure<LlmOptions>(options => BindLlmOptions(options, configuration));
        services.Configure<IngestionOptions>(options =>
        {
            configuration.GetSection("Ingestion").Bind(options);
            options.MaxDegreeOfParallelism = Int(
                configuration["INGESTION_MAX_DEGREE_OF_PARALLELISM"] ?? configuration["INGESTION_MAX_PARALLELISM"],
                options.MaxDegreeOfParallelism);
        });
        services.Configure<LocalSourceOptions>(options =>
        {
            configuration.GetSection("LocalSource").Bind(options);
            var roots = configuration["LOCAL_SOURCE_ALLOWED_ROOTS"];
            if (!string.IsNullOrWhiteSpace(roots))
            {
                options.AllowedRoots.Clear();
                foreach (var root in roots.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    options.AllowedRoots.Add(root);
                }
            }
        });
        services.Configure<CloudSourceOptions>(options =>
        {
            configuration.GetSection("CloudSource").Bind(options);
            var prefixes = configuration["CLOUD_SOURCE_ALLOWED_PREFIXES"];
            if (!string.IsNullOrWhiteSpace(prefixes))
            {
                options.AllowedPrefixes.Clear();

                // Comma-separated, not Path.PathSeparator: on Unix that separator is ':', which
                // appears in every "s3://" and "azureblob://" prefix.
                foreach (var prefix in prefixes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    options.AllowedPrefixes.Add(prefix);
                }
            }
        });
        services.Configure<JobStoreOptions>(options =>
        {
            configuration.GetSection("JobStore").Bind(options);
            options.Provider = configuration["JOB_STORE"] ?? options.Provider;
            options.ConnectionString = configuration["MONGO_CONNECTION_STRING"] ?? options.ConnectionString;
            options.DatabaseName = configuration["MONGO_DATABASE"] ?? options.DatabaseName;
            options.CollectionName = configuration["MONGO_JOBS_COLLECTION"] ?? options.CollectionName;
        });
        services.Configure<DocumentStoreOptions>(options =>
        {
            configuration.GetSection("DocumentStore").Bind(options);
            options.Provider = configuration["DOC_STORE"] ?? options.Provider;
            options.ConnectionString = configuration["MONGO_CONNECTION_STRING"] ?? configuration["COSMOS_CONNECTION_STRING"] ?? options.ConnectionString;
            options.Endpoint = configuration["COSMOS_ENDPOINT"] ?? options.Endpoint;
            options.Key = configuration["COSMOS_KEY"] ?? options.Key;
            options.DatabaseName = configuration["MONGO_DATABASE"] ?? configuration["COSMOS_DATABASE"] ?? options.DatabaseName;
            options.ContainerName = configuration["MONGO_CHUNKS_COLLECTION"] ?? configuration["COSMOS_CHUNKS_CONTAINER"] ?? options.ContainerName;
            options.DocumentsContainerName = configuration["MONGO_DOCUMENTS_COLLECTION"] ?? configuration["COSMOS_DOCUMENTS_CONTAINER"] ?? options.DocumentsContainerName;
        });
        services.Configure<VectorStoreOptions>(options =>
        {
            configuration.GetSection("VectorStore").Bind(options);
            options.Provider = configuration["VECTOR_STORE"] ?? options.Provider;
            options.Endpoint = configuration["ELASTICSEARCH_URI"] ?? options.Endpoint;
            options.Username = configuration["ELASTICSEARCH_USERNAME"] ?? options.Username;
            options.Password = configuration["ELASTICSEARCH_PASSWORD"] ?? options.Password;
            options.IndexName = configuration["ELASTICSEARCH_INDEX"] ?? options.IndexName;
            options.Dimensions = Int(configuration["ELASTICSEARCH_VECTOR_DIMENSIONS"], options.Dimensions);
        });
        services.Configure<ApiOptions>(options =>
        {
            configuration.GetSection("Api").Bind(options);
            options.ApiKey = configuration["RAG_API_KEY"] ?? options.ApiKey;
        });

        var llmHttpOptions = BuildLlmOptions(configuration);
        var llmAttemptTimeout = TimeSpan.FromSeconds(llmHttpOptions.TimeoutSeconds);
        var llmRetryCount = llmHttpOptions.RetryCount;
        var llmRetryBackoff = TimeSpan.FromSeconds(llmHttpOptions.RetryBackoffSeconds);
        var llmTotalTimeout = TimeSpan.FromTicks((llmAttemptTimeout.Ticks * (llmRetryCount + 1)) + (llmRetryBackoff.Ticks * llmRetryCount));
        var llmCircuitBreakerSamplingDuration = TimeSpan.FromTicks(llmAttemptTimeout.Ticks * 2);

        services.AddHttpClient("rag-llm", client =>
        {
            client.Timeout = llmTotalTimeout;
        }).AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = llmAttemptTimeout;
            options.TotalRequestTimeout.Timeout = llmTotalTimeout;
            options.Retry.MaxRetryAttempts = llmRetryCount;
            options.Retry.Delay = llmRetryBackoff;
            options.CircuitBreaker.SamplingDuration = Max(
                options.CircuitBreaker.SamplingDuration,
                llmCircuitBreakerSamplingDuration);
        });
        services.AddHttpClient("rag-elasticsearch").AddStandardResilienceHandler();

        services.AddSingleton<IDocumentParser, TxtDocumentParser>();
        services.AddSingleton<IDocumentParser, MarkdownDocumentParser>();
        services.AddSingleton<IDocumentParser, PdfDocumentParser>();
        services.AddSingleton<IDocumentParser, HtmlDocumentParser>();
        services.AddSingleton<IMultiDocumentParser, JsonlDocumentParser>();
        services.AddSingleton<IMultiDocumentParser, CsvDocumentParser>();
        services.AddSingleton<IDocumentParserResolver, DocumentParserResolver>();

        // AWS S3 and Azure Blob sources are opt-in: Rag.Providers.Aws / Rag.Providers.AzureBlob add
        // themselves to this collection through AddRagAwsS3 / AddRagAzureBlob when referenced.
        services.AddSingleton<IDocumentSource, LocalDirectorySource>();
        services.AddSingleton<IDocumentSourceResolver, DocumentSourceResolver>();

        services.AddSingleton<IChunkingStrategy, FixedChunkingStrategy>();
        services.AddSingleton<IChunkingStrategy, RecursiveChunkingStrategy>();
        services.AddSingleton<IChunkingStrategy, MarkdownAwareChunkingStrategy>();
        services.AddSingleton<IChunkingStrategy, SemanticChunkingStrategy>();
        services.AddSingleton<IChunkingStrategyFactory, ChunkingStrategyFactory>();

        services.AddSingleton<DeterministicLlmClient>();
        services.AddSingleton<HttpLlmClient>();
        services.AddSingleton<IEmbeddingClient>(sp => ResolveLlm(sp).Embedding);
        services.AddSingleton<IChatClient>(sp => ResolveLlm(sp).Chat);

        // "mongo" and "cosmos" are not registered here: Rag.Providers.Mongo / Rag.Providers.Cosmos
        // add themselves as IDocumentStore keyed singletons ("mongo" / "cosmos") through AddRagMongo
        // / AddRagCosmos when referenced. ResolveDocumentStore looks them up by key so Rag.Core never
        // names those concrete types.
        services.AddSingleton<InMemoryDocumentStore>();
        services.AddSingleton<FileDocumentStore>();
        services.AddSingleton<IDocumentStore>(ResolveDocumentStore);

        services.AddSingleton<InMemoryVectorStore>();
        services.AddSingleton<ElasticsearchVectorStore>();
        services.AddSingleton<AzureAiSearchVectorStoreStub>();
        services.AddSingleton<IAzureAiSearchVectorStore>(sp => sp.GetRequiredService<AzureAiSearchVectorStoreStub>());
        services.AddSingleton<IVectorStore>(ResolveVectorStore);

        services.AddSingleton<IIngestionPipeline, IngestionPipeline>();
        services.AddSingleton<IQueryPipeline, QueryPipeline>();
        services.AddSingleton<IChunkPreviewService, ChunkPreviewService>();

        // "mongo" is not registered here: Rag.Providers.Mongo adds itself as an IIngestionJobStore
        // keyed singleton ("mongo") through AddRagMongo when referenced. ResolveJobStore looks it up
        // by key so Rag.Core never names that concrete type.
        services.AddSingleton<InMemoryIngestionJobStore>();
        services.AddSingleton<IIngestionJobStore>(ResolveJobStore);
        services.AddSingleton<IIngestionJobQueue, IngestionJobQueue>();

        return services;
    }

    /// <summary>
    /// Opt-in registration for the background ingestion worker. <see cref="IngestionBackgroundService"/>
    /// is a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>, which only runs under an
    /// <see cref="Microsoft.Extensions.Hosting.IHost"/>. The API hosts one and should call this right
    /// after <see cref="AddRagPlatform"/>. The CLI builds a plain <see cref="IServiceCollection"/>/
    /// <see cref="IServiceProvider"/> with no host, so it must not call this: the hosted service would
    /// never run there, and it ingests inline instead.
    /// </summary>
    public static IServiceCollection AddRagIngestionWorker(this IServiceCollection services)
    {
        services.AddHostedService<IngestionBackgroundService>();
        return services;
    }

    private static (IEmbeddingClient Embedding, IChatClient Chat) ResolveLlm(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IOptions<LlmOptions>>().Value.Provider;
        if (string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "azure-openai", StringComparison.OrdinalIgnoreCase))
        {
            var client = services.GetRequiredService<HttpLlmClient>();
            return (client, client);
        }

        var deterministic = services.GetRequiredService<DeterministicLlmClient>();
        return (deterministic, deterministic);
    }

    /// <summary>
    /// Resolves the configured document store. "memory" and "file" are implemented directly in
    /// Rag.Core; every other name is looked up as a keyed <see cref="IDocumentStore"/> registered by
    /// an opt-in provider package (for example Rag.Providers.Mongo's <c>AddRagMongo</c>). An
    /// explicitly requested provider whose package was never referenced throws immediately, naming
    /// the missing package, rather than silently falling back to memory.
    /// </summary>
    private static IDocumentStore ResolveDocumentStore(IServiceProvider services)
    {
        var name = NormalizeProviderName(services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.Provider);
        if (name == "memory")
        {
            return services.GetRequiredService<InMemoryDocumentStore>();
        }

        if (name == "file")
        {
            return services.GetRequiredService<FileDocumentStore>();
        }

        return services.GetKeyedService<IDocumentStore>(name) ?? throw DocumentStoreUnavailable(name);
    }

    private static InvalidOperationException DocumentStoreUnavailable(string name)
    {
        return name switch
        {
            "mongo" => new InvalidOperationException(
                "DOC_STORE=mongo requires a reference to Rag.Providers.Mongo and a call to AddRagMongo(configuration)."),
            "cosmos" => new InvalidOperationException(
                "DOC_STORE=cosmos requires a reference to Rag.Providers.Cosmos and a call to AddRagCosmos(configuration)."),
            _ => new InvalidOperationException(
                $"DOC_STORE={name} is not a supported document store provider. Supported values: memory, file, mongo (Rag.Providers.Mongo), cosmos (Rag.Providers.Cosmos).")
        };
    }

    /// <summary>
    /// Resolves the configured vector store. "memory" and "elasticsearch" are both implemented
    /// directly in Rag.Core (Elasticsearch talks raw HTTP through <c>IHttpClientFactory</c> and needs
    /// no provider SDK), so an explicitly requested value that is neither throws rather than
    /// silently falling back to memory.
    /// </summary>
    private static IVectorStore ResolveVectorStore(IServiceProvider services)
    {
        var name = NormalizeProviderName(services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Provider);
        return name switch
        {
            "memory" => services.GetRequiredService<InMemoryVectorStore>(),
            "elasticsearch" => services.GetRequiredService<ElasticsearchVectorStore>(),
            _ => services.GetKeyedService<IVectorStore>(name) ?? throw new InvalidOperationException(
                $"VECTOR_STORE={name} is not a supported vector store provider. Supported values: memory, elasticsearch.")
        };
    }

    /// <summary>
    /// Resolves the configured ingestion job store. "memory" is implemented directly in Rag.Core;
    /// "mongo" is looked up as a keyed <see cref="IIngestionJobStore"/> registered by
    /// Rag.Providers.Mongo's <c>AddRagMongo</c>. An explicitly requested provider whose package was
    /// never referenced throws immediately, naming the missing package.
    /// </summary>
    private static IIngestionJobStore ResolveJobStore(IServiceProvider services)
    {
        var name = NormalizeProviderName(services.GetRequiredService<IOptions<JobStoreOptions>>().Value.Provider);
        if (name == "memory")
        {
            return services.GetRequiredService<InMemoryIngestionJobStore>();
        }

        return services.GetKeyedService<IIngestionJobStore>(name) ?? throw (name == "mongo"
            ? new InvalidOperationException(
                "JOB_STORE=mongo requires a reference to Rag.Providers.Mongo and a call to AddRagMongo(configuration).")
            : new InvalidOperationException(
                $"JOB_STORE={name} is not a supported job store provider. Supported values: memory, mongo (Rag.Providers.Mongo)."));
    }

    /// <summary>
    /// Treats an unset or blank provider value as "memory", matching every options class's own
    /// default; any other value is normalized for case-insensitive comparison and keyed lookup.
    /// </summary>
    private static string NormalizeProviderName(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider) ? "memory" : provider.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Single definition of how <see cref="LlmOptions"/> is bound. The resilience handler needs the
    /// timeout and retry values eagerly at registration time while the rest of the platform reads
    /// them through <see cref="IOptions{TOptions}"/>, so both paths run this same method rather than
    /// each restating the configuration key fallbacks.
    /// </summary>
    private static void BindLlmOptions(LlmOptions options, IConfiguration configuration)
    {
        configuration.GetSection("Llm").Bind(options);
        options.Provider = configuration["LLM_PROVIDER"] ?? options.Provider;
        options.ApiKey = configuration["LLM_API_KEY"] ?? options.ApiKey;
        options.EmbeddingEndpoint = configuration["LLM_EMBEDDING_ENDPOINT"] ?? options.EmbeddingEndpoint;
        options.EmbeddingModel = configuration["LLM_EMBEDDING_MODEL"] ?? options.EmbeddingModel;
        options.EmbeddingDimensions = Int(configuration["LLM_EMBEDDING_DIMENSIONS"], options.EmbeddingDimensions);
        options.ChatEndpoint = configuration["LLM_CHAT_ENDPOINT"] ?? options.ChatEndpoint;
        options.ChatModel = configuration["LLM_CHAT_MODEL"] ?? options.ChatModel;
        options.SystemPrompt = configuration["LLM_SYSTEM_PROMPT"] ?? options.SystemPrompt;
        options.TimeoutSeconds = PositiveInt(configuration["LLM_TIMEOUT_SECONDS"] ?? configuration["HTTP_TIMEOUT_SECONDS"], options.TimeoutSeconds);
        options.RetryCount = PositiveInt(configuration["LLM_RETRY_COUNT"] ?? configuration["HTTP_RETRY_COUNT"], options.RetryCount);
        options.RetryBackoffSeconds = PositiveInt(configuration["LLM_RETRY_BACKOFF_SECONDS"] ?? configuration["HTTP_RETRY_BACKOFF_SECONDS"], options.RetryBackoffSeconds);
    }

    private static LlmOptions BuildLlmOptions(IConfiguration configuration)
    {
        var options = new LlmOptions();
        BindLlmOptions(options, configuration);
        return options;
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second)
    {
        return first >= second ? first : second;
    }

    private static int Int(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int PositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static double Double(string? value, double fallback)
    {
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }
}
