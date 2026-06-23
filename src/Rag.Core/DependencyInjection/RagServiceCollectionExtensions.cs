using Amazon;
using Amazon.S3;
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
            options.ApiKey = configuration["LLM_API_KEY"] ?? options.ApiKey;
            options.EmbeddingEndpoint = configuration["LLM_EMBEDDING_ENDPOINT"] ?? options.EmbeddingEndpoint;
            options.EmbeddingModel = configuration["LLM_EMBEDDING_MODEL"] ?? options.EmbeddingModel;
            options.EmbeddingDimensions = Int(configuration["LLM_EMBEDDING_DIMENSIONS"], options.EmbeddingDimensions);
            options.ChatEndpoint = configuration["LLM_CHAT_ENDPOINT"] ?? options.ChatEndpoint;
            options.ChatModel = configuration["LLM_CHAT_MODEL"] ?? options.ChatModel;
            options.SystemPrompt = configuration["LLM_SYSTEM_PROMPT"] ?? options.SystemPrompt;
        });
        services.Configure<IngestionOptions>(options =>
        {
            configuration.GetSection("Ingestion").Bind(options);
            options.MaxDegreeOfParallelism = Int(
                configuration["INGESTION_MAX_DEGREE_OF_PARALLELISM"] ?? configuration["INGESTION_MAX_PARALLELISM"],
                options.MaxDegreeOfParallelism);
        });
        services.Configure<JobStoreOptions>(options =>
        {
            configuration.GetSection("JobStore").Bind(options);
            options.Provider = configuration["JOB_STORE"] ?? options.Provider;
            options.ConnectionString = configuration["MONGO_CONNECTION_STRING"] ?? options.ConnectionString;
            options.DatabaseName = configuration["MONGO_DATABASE"] ?? options.DatabaseName;
            options.CollectionName = configuration["MONGO_JOBS_COLLECTION"] ?? options.CollectionName;
        });
        services.Configure<S3Options>(options =>
        {
            configuration.GetSection("S3").Bind(options);
            options.Region = configuration["S3_REGION"] ?? configuration["AWS_REGION"] ?? configuration["AWS_DEFAULT_REGION"] ?? options.Region;
            options.ServiceUrl = configuration["S3_ENDPOINT"] ?? configuration["S3_SERVICE_URL"] ?? configuration["AWS_ENDPOINT_URL"] ?? options.ServiceUrl;
            options.ForcePathStyle = Bool(configuration["S3_FORCE_PATH_STYLE"], options.ForcePathStyle);
        });
        services.Configure<AzureBlobOptions>(options =>
        {
            configuration.GetSection("AzureBlob").Bind(options);
            options.ConnectionString = configuration["AZURE_BLOB_CONNECTION_STRING"] ?? options.ConnectionString;
            options.ServiceUri = configuration["AZURE_BLOB_SERVICE_URI"] ?? configuration["AZURE_BLOB_ENDPOINT"] ?? options.ServiceUri;
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

        services.AddHttpClient("rag-llm").AddStandardResilienceHandler();
        services.AddHttpClient("rag-elasticsearch").AddStandardResilienceHandler();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var s3Options = sp.GetRequiredService<IOptions<S3Options>>().Value;
            var config = new AmazonS3Config
            {
                ForcePathStyle = s3Options.ForcePathStyle
            };
            if (!string.IsNullOrWhiteSpace(s3Options.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region);
            }

            if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
            {
                config.ServiceURL = s3Options.ServiceUrl;
            }

            return new AmazonS3Client(config);
        });

        services.AddSingleton<IDocumentParser, TxtDocumentParser>();
        services.AddSingleton<IDocumentParser, MarkdownDocumentParser>();
        services.AddSingleton<IDocumentParser, PdfDocumentParser>();
        services.AddSingleton<IDocumentParser, HtmlDocumentParser>();
        services.AddSingleton<IMultiDocumentParser, JsonlDocumentParser>();
        services.AddSingleton<IMultiDocumentParser, CsvDocumentParser>();
        services.AddSingleton<IDocumentParserResolver, DocumentParserResolver>();

        services.AddSingleton<IDocumentSource, LocalDirectorySource>();
        services.AddSingleton<IDocumentSource, AwsS3DocumentSource>();
        services.AddSingleton<IDocumentSource, AzureBlobDocumentSource>();
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
        services.AddSingleton<InMemoryIngestionJobStore>();
        services.AddSingleton<MongoIngestionJobStore>();
        services.AddSingleton<IIngestionJobStore>(ResolveJobStore);
        services.AddSingleton<IIngestionJobQueue, IngestionJobQueue>();
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

    private static IIngestionJobStore ResolveJobStore(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IOptions<JobStoreOptions>>().Value.Provider;
        return provider.ToLowerInvariant() switch
        {
            "mongo" => services.GetRequiredService<MongoIngestionJobStore>(),
            _ => services.GetRequiredService<InMemoryIngestionJobStore>()
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
