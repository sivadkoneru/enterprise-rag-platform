using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Workbench;

namespace Rag.Api.Workbench;

public sealed class WorkbenchEnvironment(IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment, IHttpClientFactory clients)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<JsonNode> ConfigurationAsync(CancellationToken token)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "configuration-catalog.json");
        using var stream = File.OpenRead(path);
        var catalog = await JsonNode.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false) ?? throw new InvalidDataException("Configuration catalog is unavailable.");
        var sections = new Dictionary<string, object>
        {
            ["Query"] = services.GetRequiredService<IOptions<WorkbenchQueryOptions>>().Value,
            ["Rag"] = services.GetRequiredService<IOptions<RagOptions>>().Value,
            ["Chunking"] = services.GetRequiredService<IOptions<ChunkingOptions>>().Value,
            ["Llm"] = services.GetRequiredService<IOptions<LlmOptions>>().Value,
            ["DocumentStore"] = services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value,
            ["VectorStore"] = services.GetRequiredService<IOptions<VectorStoreOptions>>().Value,
            ["JobStore"] = services.GetRequiredService<IOptions<JobStoreOptions>>().Value,
            ["Api"] = services.GetRequiredService<IOptions<ApiOptions>>().Value,
            ["LocalSource"] = services.GetRequiredService<IOptions<LocalSourceOptions>>().Value,
            ["CloudSource"] = services.GetRequiredService<IOptions<CloudSourceOptions>>().Value,
            ["Ingestion"] = services.GetRequiredService<IOptions<IngestionOptions>>().Value,
            ["Reranker"] = services.GetRequiredService<IOptions<RerankerOptions>>().Value
        }.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToNode(pair.Value, Json));
        foreach (var group in catalog["groups"]?.AsArray() ?? [])
        {
            foreach (var field in group?["fields"]?.AsArray() ?? [])
            {
                if (field is null) { continue; }
                var section = field["section"]?.GetValue<string>();
                var option = field["option"]?.GetValue<string>();
                var key = field["key"]?.GetValue<string>() ?? "";
                JsonNode? value = null;
                if (section is not null && option is not null && sections.TryGetValue(section, out var values)) { value = values?[JsonNamingPolicy.CamelCase.ConvertName(option)]?.DeepClone(); }
                else
                {
                    var raw = configuration[key];
                    if (key == "ASPNETCORE_ENVIRONMENT") { raw = environment.EnvironmentName; }
                    value = raw is null ? null : JsonValue.Create(raw);
                }
                if (value is JsonArray array) { value = JsonValue.Create(string.Join(Environment.NewLine, array.Select(item => item?.ToString()))); }
                var configured = value is not null && !string.IsNullOrWhiteSpace(value.ToString());
                var secret = field["secret"]?.GetValue<bool>() == true || option is "ApiKey" or "Key" or "Password" or "ConnectionString" || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase);
                field["configured"] = configured;
                if (!secret && value is JsonValue && Uri.TryCreate(value.ToString(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                {
                    var sanitized = new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" };
                    value = JsonValue.Create(sanitized.Uri.ToString());
                }
                field["value"] = secret ? null : value;
            }
        }
        return catalog;
    }
    public async Task<object> CheckProviderAsync(string kind, CancellationToken token)
    {
        if (kind is not ("embedding" or "chat" or "reranker")) { throw new ArgumentException("Choose embedding, chat, or reranker."); }
        var llm = services.GetRequiredService<IOptions<LlmOptions>>().Value;
        var rerankerOptions = services.GetRequiredService<IOptions<RerankerOptions>>().Value;
        if (kind == "reranker" && string.IsNullOrWhiteSpace(rerankerOptions.Endpoint) || kind == "embedding" && llm.Provider != "deterministic" && string.IsNullOrWhiteSpace(llm.EmbeddingEndpoint) || kind == "chat" && llm.Provider != "deterministic" && string.IsNullOrWhiteSpace(llm.ChatEndpoint))
        {
            return new { name = kind, status = "not-configured", message = "Configure the selected provider endpoint.", latencyMs = 0, details = new Dictionary<string, object?>() };
        }
        var clock = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(llm.TimeoutSeconds, 1, 60)));
        try
        {
            var details = new Dictionary<string, object?>();
            if (kind == "embedding")
            {
                var vector = await services.GetRequiredService<IEmbeddingClient>().EmbedAsync("Readiness check.", timeout.Token).ConfigureAwait(false);
                if (vector.Count != llm.EmbeddingDimensions || vector.Any(value => !float.IsFinite(value))) { throw new InvalidDataException("Embedding dimensions or values are invalid."); }
                details["dimensions"] = vector.Count;
                details["model"] = WorkbenchModelIdentity.EmbeddingModel(llm);
            }
            else if (kind == "chat")
            {
                var result = await services.GetRequiredService<IChatClient>().CompleteAsync([new Rag.Core.Models.ChatMessage("user", "Reply with OK.")], timeout.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(result)) { throw new InvalidDataException("Chat provider returned no content."); }
                details["outputCharacters"] = result.Length;
                details["model"] = WorkbenchModelIdentity.ChatModel(llm);
            }
            else
            {
                var scores = await services.GetRequiredService<IRerankerClient>().RerankAsync("What is the refund window?", ["A direct purchase can be refunded within thirty days.", "API access requires a scoped credential."], timeout.Token).ConfigureAwait(false);
                details["scoredDocuments"] = scores.Count;
                details["model"] = rerankerOptions.Model;
            }
            return new { name = kind, status = "healthy", message = "A real request to the configured provider succeeded.", latencyMs = clock.ElapsedMilliseconds, details };
        }
        catch (Exception exception) when (!token.IsCancellationRequested && exception is not OutOfMemoryException)
        {
            return new { name = kind, status = "failed", message = "The configured provider request failed. Check endpoint, model, credentials, and server diagnostics.", latencyMs = clock.ElapsedMilliseconds, details = new Dictionary<string, object?>() };
        }
    }
    public async Task<object> ReadinessAsync(CancellationToken token)
    {
        var checks = new List<ReadinessCheck>();
        async Task CheckAsync(string id, Func<CancellationToken, Task> probe)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try { await probe(timeout.Token).ConfigureAwait(false); checks.Add(new ReadinessCheck(id, "ready", "Connection verified.")); }
            catch (Exception exception) when (!token.IsCancellationRequested && exception is not OutOfMemoryException) { checks.Add(new ReadinessCheck(id, "unavailable", "Connection could not be verified; inspect server configuration and diagnostics.")); }
        }
        await CheckAsync("document-store", async cancellation => { await services.GetRequiredService<IDocumentStore>().GetChunksAsync(["readiness-probe"], cancellation).ConfigureAwait(false); }).ConfigureAwait(false);
        await CheckAsync("catalog-store", async cancellation => { await services.GetRequiredService<IWorkbenchStateStore>().GetAsync<WorkbenchCorpus>("corpora", "readiness-probe", cancellation).ConfigureAwait(false); }).ConfigureAwait(false);
        await CheckAsync("job-store", async cancellation => { await services.GetRequiredService<IIngestionJobStore>().GetAsync("readiness-probe", cancellation).ConfigureAwait(false); }).ConfigureAwait(false);
        await CheckAsync("vector-store", async cancellation =>
        {
            var options = services.GetRequiredService<IOptions<VectorStoreOptions>>().Value;
            if (options.Provider.Equals("elasticsearch", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)) { throw new InvalidOperationException("Configure the vector store endpoint."); }
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "/_cluster/health"));
                if (!string.IsNullOrWhiteSpace(options.Username)) { request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"))); }
                using var response = await clients.CreateClient("rag-readiness").SendAsync(request, cancellation).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            else
            {
                var vector = new float[options.Dimensions];
                vector[0] = 1;
                await services.GetRequiredService<IVectorStore>().SearchAsync(vector, 1, new Rag.Core.Models.VectorSearchFilter(DocumentIds: ["readiness-probe"]), cancellation).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
        var llm = services.GetRequiredService<IOptions<LlmOptions>>().Value;
        if (llm.Provider.Equals("deterministic", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new ReadinessCheck("embedding", "ready", "Deterministic local provider; no external requests."));
            checks.Add(new ReadinessCheck("chat", "ready", "Deterministic local provider; no external requests."));
        }
        else
        {
            checks.Add(ConfiguredEndpoint("embedding", llm.EmbeddingEndpoint));
            checks.Add(ConfiguredEndpoint("chat", llm.ChatEndpoint));
        }
        var reranker = services.GetRequiredService<IOptions<RerankerOptions>>().Value;
        checks.Add(ConfiguredEndpoint("reranker", reranker.Endpoint));
        return new { ready = checks.Where(check => check.Id != "reranker").All(check => check.Status is "ready" or "configured"), checks };
    }
    private static ReadinessCheck ConfiguredEndpoint(string id, string? endpoint)
    {
        var configured = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
        return new ReadinessCheck(id, configured ? "configured" : "unconfigured", configured ? "Configured; use the explicit provider test to verify credentials and inference readiness." : "Configure an HTTP endpoint.");
    }
    private sealed record ReadinessCheck(string Id, string Status, string Detail);
}
