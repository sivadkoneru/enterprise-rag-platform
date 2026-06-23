using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Chunking;
using Rag.Core.Configuration;
using Rag.Core.Llm;
using Rag.Core.Parsing;
using Rag.Core.Pipelines;
using Rag.Core.Stores;
using Rag.Core.Vector;

namespace Rag.Core.DependencyInjection;

public static class RagServiceCollectionExtensions
{
    public static IServiceCollection AddRagPlatform(this IServiceCollection services, IConfiguration configuration)
    {
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
            options.SemanticDistanceThreshold = Double(
                configuration["SEMANTIC_DISTANCE_THRESHOLD"] ?? configuration["SEMANTIC_CHUNKING_SIMILARITY_THRESHOLD"],
                options.SemanticDistanceThreshold);
            options.SemanticRefineWithChat = Bool(configuration["SEMANTIC_CHUNKING_LLM_REFINEMENT_ENABLED"], options.SemanticRefineWithChat);
        });
        services.Configure<LlmOptions>(options =>
        {
            configuration.GetSection("Llm").Bind(options);
            options.Provider = configuration["LLM_PROVIDER"] ?? options.Provider;
            options.ApiKey = configuration["OPENAI_API_KEY"] ?? configuration["AZURE_OPENAI_API_KEY"] ?? options.ApiKey;
            options.EmbeddingEndpoint = configuration["OPENAI_EMBEDDING_ENDPOINT"] ?? configuration["AZURE_OPENAI_EMBEDDING_ENDPOINT"] ?? options.EmbeddingEndpoint;
            options.EmbeddingModel = configuration["OPENAI_EMBEDDING_MODEL"] ?? configuration["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"] ?? options.EmbeddingModel;
            options.EmbeddingDimensions = Int(configuration["OPENAI_EMBEDDING_DIMENSIONS"] ?? configuration["AZURE_OPENAI_EMBEDDING_DIMENSIONS"], options.EmbeddingDimensions);
            options.ChatEndpoint = configuration["OPENAI_CHAT_ENDPOINT"] ?? configuration["AZURE_OPENAI_CHAT_ENDPOINT"] ?? options.ChatEndpoint;
            options.ChatModel = configuration["OPENAI_CHAT_MODEL"] ?? configuration["AZURE_OPENAI_CHAT_DEPLOYMENT"] ?? options.ChatModel;
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
        });
        services.Configure<VectorStoreOptions>(options =>
        {
            configuration.GetSection("VectorStore").Bind(options);
            options.Provider = configuration["VECTOR_STORE"] ?? options.Provider;
            options.Endpoint = configuration["ELASTICSEARCH_URI"] ?? options.Endpoint;
            options.IndexName = configuration["ELASTICSEARCH_INDEX"] ?? options.IndexName;
            options.Dimensions = Int(configuration["ELASTICSEARCH_VECTOR_DIMENSIONS"], options.Dimensions);
        });

        services.AddHttpClient("rag-llm");
        services.AddHttpClient("rag-elasticsearch");

        services.AddSingleton<IDocumentParser, TxtDocumentParser>();
        services.AddSingleton<IDocumentParser, MarkdownDocumentParser>();
        services.AddSingleton<IDocumentParser, PdfDocumentParser>();
        services.AddSingleton<IDocumentParserResolver, DocumentParserResolver>();

        services.AddSingleton<IChunkingStrategy, FixedChunkingStrategy>();
        services.AddSingleton<IChunkingStrategy, RecursiveChunkingStrategy>();
        services.AddSingleton<IChunkingStrategy, MarkdownAwareChunkingStrategy>();
        services.AddSingleton<IChunkingStrategy, SemanticChunkingStrategy>();
        services.AddSingleton<IChunkingStrategyFactory, ChunkingStrategyFactory>();

        services.AddSingleton<DeterministicLlmClient>();
        services.AddSingleton<HttpLlmClient>();
        services.AddSingleton<IEmbeddingClient>(sp => ResolveLlm(sp).Embedding);
        services.AddSingleton<IChatClient>(sp => ResolveLlm(sp).Chat);

        services.AddSingleton<InMemoryDocumentStore>();
        services.AddSingleton<FileDocumentStore>();
        services.AddSingleton<MongoDocumentStore>();
        services.AddSingleton<CosmosDocumentStore>();
        services.AddSingleton<IDocumentStore>(ResolveDocumentStore);

        services.AddSingleton<InMemoryVectorStore>();
        services.AddSingleton<ElasticsearchVectorStore>();
        services.AddSingleton<AzureAiSearchVectorStoreStub>();
        services.AddSingleton<IAzureAiSearchVectorStore>(sp => sp.GetRequiredService<AzureAiSearchVectorStoreStub>());
        services.AddSingleton<IVectorStore>(ResolveVectorStore);

        services.AddSingleton<IIngestionPipeline, IngestionPipeline>();
        services.AddSingleton<IQueryPipeline, QueryPipeline>();
        services.AddSingleton<IChunkPreviewService, ChunkPreviewService>();

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

    private static IDocumentStore ResolveDocumentStore(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.Provider;
        return provider.ToLowerInvariant() switch
        {
            "mongo" => services.GetRequiredService<MongoDocumentStore>(),
            "cosmos" => services.GetRequiredService<CosmosDocumentStore>(),
            "file" => services.GetRequiredService<FileDocumentStore>(),
            _ => services.GetRequiredService<InMemoryDocumentStore>()
        };
    }

    private static IVectorStore ResolveVectorStore(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IOptions<VectorStoreOptions>>().Value.Provider;
        return provider.ToLowerInvariant() switch
        {
            "elasticsearch" => services.GetRequiredService<ElasticsearchVectorStore>(),
            "memory" => services.GetRequiredService<InMemoryVectorStore>(),
            _ => services.GetRequiredService<InMemoryVectorStore>()
        };
    }

    private static int Int(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static double Double(string? value, double fallback)
    {
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool Bool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
