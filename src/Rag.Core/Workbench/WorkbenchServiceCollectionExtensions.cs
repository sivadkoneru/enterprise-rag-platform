using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;

namespace Rag.Core.Workbench;

public static class WorkbenchServiceCollectionExtensions
{
    public static IServiceCollection AddRagWorkbench(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var key in new[] { "QUERY_TOP_K", "QUERY_MAX_CONTEXT_TOKENS", "RERANKER_TIMEOUT_SECONDS" })
        {
            if (configuration[key] is { } raw && !int.TryParse(raw, out _)) { throw new ArgumentException($"{key} must be an integer."); }
        }
        foreach (var key in new[] { "QUERY_RERANKER", "QUERY_NEIGHBORS" })
        {
            if (configuration[key] is { } raw && !bool.TryParse(raw, out _)) { throw new ArgumentException($"{key} must be true or false."); }
        }
        if (configuration["QUERY_MIN_RELEVANCE"] is { } threshold && (!double.TryParse(threshold, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedThreshold) || !double.IsFinite(parsedThreshold)))
        { throw new ArgumentException("QUERY_MIN_RELEVANCE must be a finite number."); }
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
        services.AddOptions<WorkbenchQueryOptions>().Validate(options => options.TopK is >= 1 and <= 100 && options.Mode is "vector" or "hybrid" && double.IsFinite(options.MinRelevance) && options.MinRelevance is >= 0 and <= 1 && options.MaxContextTokens is >= 128 and <= 65536, "Invalid workbench query defaults.").ValidateOnStart();
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
        services.AddHttpClient("rag-reranker").RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient("rag-readiness", client => client.Timeout = TimeSpan.FromSeconds(5)).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
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
