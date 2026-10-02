using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;

namespace Rag.Core.Workbench;

public static class WorkbenchServiceCollectionExtensions
{
    public static IServiceCollection AddRagWorkbench(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkbenchQueryOptions>(options =>
        {
            configuration.GetSection("Query").Bind(options);
            if (int.TryParse(configuration["QUERY_TOP_K"], out var topK)) { options.TopK = topK; }
            options.Mode = configuration["QUERY_MODE"] ?? options.Mode;
            if (bool.TryParse(configuration["QUERY_RERANKER"], out var enabled)) { options.Reranker = enabled; }
            if (double.TryParse(configuration["QUERY_MIN_RELEVANCE"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var relevance)) { options.MinRelevance = relevance; }
            if (bool.TryParse(configuration["QUERY_NEIGHBORS"], out var neighbors)) { options.Neighbors = neighbors; }
            if (int.TryParse(configuration["QUERY_MAX_CONTEXT_TOKENS"], out var budget)) { options.MaxContextTokens = budget; }
        });
        services.Configure<RerankerOptions>(options =>
        {
            configuration.GetSection("Reranker").Bind(options);
            options.Endpoint = configuration["RERANKER_ENDPOINT"] ?? options.Endpoint;
            options.ApiKey = configuration["RERANKER_API_KEY"] ?? options.ApiKey;
            options.Model = configuration["RERANKER_MODEL"] ?? options.Model;
            if (int.TryParse(configuration["RERANKER_TIMEOUT_SECONDS"], out var timeout))
            {
                options.TimeoutSeconds = timeout;
            }
        });
        services.AddHttpClient("rag-reranker");
        services.AddHttpClient("rag-readiness", client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<IRerankerClient, HttpRerankerClient>();
        services.AddSingleton<InMemoryWorkbenchStateStore>();
        services.AddSingleton<InMemoryWorkbenchJobStateStore>();
        services.AddSingleton<FileWorkbenchStateStore>();
        services.AddSingleton<IWorkbenchStateStore>(provider =>
        {
            var name = provider.GetRequiredService<IOptions<DocumentStoreOptions>>().Value.Provider.Trim().ToLowerInvariant();
            IWorkbenchStateStore catalog = name switch
            {
                "memory" => provider.GetRequiredService<InMemoryWorkbenchStateStore>(),
                "file" => provider.GetRequiredService<FileWorkbenchStateStore>(),
                _ => provider.GetKeyedService<IWorkbenchStateStore>(name) ?? throw new InvalidOperationException($"Workbench persistence for DOC_STORE={name} requires its provider package and AddRag provider registration.")
            };
            var jobName = provider.GetRequiredService<IOptions<JobStoreOptions>>().Value.Provider.Trim().ToLowerInvariant();
            IWorkbenchJobStateStore jobs = jobName == "memory" ? provider.GetRequiredService<InMemoryWorkbenchJobStateStore>() : provider.GetKeyedService<IWorkbenchJobStateStore>(jobName) ?? throw new InvalidOperationException($"Workbench JOB_STORE={jobName} requires the corresponding provider package and registration.");
            return new WorkbenchCompositeStateStore(catalog, jobs);
        });
        services.AddSingleton<WorkbenchVectorStores>();
        services.AddSingleton<WorkbenchCatalog>();
        services.AddSingleton<WorkbenchIngestor>();
        services.AddSingleton<DetailedQueryPipeline>();
        services.AddSingleton<WorkbenchJobs>();
        services.AddHostedService(provider => provider.GetRequiredService<WorkbenchJobs>());
        return services;
    }
}
