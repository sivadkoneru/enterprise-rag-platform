using System.Security.Cryptography;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Rag.Api.Contracts;
using Rag.Api.Workbench;
using Rag.Core.Workbench;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using Rag.Core.Jobs;
using Rag.Providers.Aws;
using Rag.Providers.AzureBlob;
using Rag.Providers.Cosmos;
using Rag.Providers.Mongo;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration
    // Lowest precedence: the API accepts source paths over the network, so local ingestion is
    // confined to the working directory unless an operator widens it through configuration.
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["LocalSource:AllowedRoots:0"] = Directory.GetCurrentDirectory()
    })
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddInMemoryCollection(EnvFile.LoadFromWorkingDirectory())
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var configuredUrls = builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["urls"];
if (!string.IsNullOrWhiteSpace(configuredUrls))
{
    builder.WebHost.UseUrls(configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 2_000_000);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { detail = "Private workbench capacity reached. Retry later." }, token).ConfigureAwait(false);
    };
    // Single-operator budgets, shared by all callers. Origin and profile IDs are not identities.
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetConcurrencyLimiter(
            context.Request.Path.StartsWithSegments("/health") ? "health" : "work", key => new ConcurrencyLimiterOptions { PermitLimit = key == "health" ? 16 : 4, QueueLimit = 0 })),
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path.Value ?? "";
            var workload = path.Contains("/checks/", StringComparison.Ordinal) ? "checks" : path.Contains("ingest", StringComparison.Ordinal) ? "ingestion" : path.Contains("evaluations", StringComparison.Ordinal) && context.Request.Method == "POST" ? "evaluation" : path.Contains("quer", StringComparison.Ordinal) ? "query" : "metadata";
            return RateLimitPartition.GetFixedWindowLimiter(workload, key => new FixedWindowRateLimiterOptions
            { PermitLimit = key switch { "checks" => 10, "ingestion" => 10, "evaluation" => 4, "query" => 60, _ => 600 }, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
        }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Enterprise RAG API",
        Version = "v1",
        Description = "Minimal API for document ingestion, ingestion jobs, chunk previews, and grounded RAG queries."
    });
});
builder.Services.AddRagPlatform(builder.Configuration);
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService("rag-api"))
        .WithTracing(tracing => tracing.AddSource("Rag.Workbench").AddOtlpExporter())
        .WithMetrics(metrics => metrics.AddMeter("Rag.Workbench").AddOtlpExporter());
}

// Opt-in provider packages: registered here so every documented DOC_STORE / JOB_STORE / source
// scheme (mongo, cosmos, s3, azureblob) keeps working exactly as it does today. A deployment that
// wants a smaller dependency graph can drop the reference and the matching AddRag* call instead.
builder.Services.AddRagAwsS3(builder.Configuration);
builder.Services.AddRagAzureBlob(builder.Configuration);
builder.Services.AddRagCosmos(builder.Configuration);
builder.Services.AddRagMongo(builder.Configuration);

builder.Services.AddRagIngestionWorker();
builder.Services.AddRagWorkbench(builder.Configuration);
builder.Services.AddSingleton<WorkbenchEnvironment>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RagExceptionHandler>();

var app = builder.Build();
var models = app.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
if (!models.Provider.Equals("deterministic", StringComparison.OrdinalIgnoreCase) &&
    (string.IsNullOrWhiteSpace(models.EmbeddingModel) || string.IsNullOrWhiteSpace(models.ChatModel) || !DirectEndpoint(models.EmbeddingEndpoint) || !DirectEndpoint(models.ChatEndpoint)))
{ throw new InvalidOperationException("HTTP models require model identifiers and direct HTTP(S) endpoints without embedded credentials or fragments."); }
var vectorSettings = app.Services.GetRequiredService<IOptions<VectorStoreOptions>>().Value;
if (vectorSettings.Provider.Equals("elasticsearch", StringComparison.OrdinalIgnoreCase) && (!DirectEndpoint(vectorSettings.Endpoint) || vectorSettings.Dimensions != models.EmbeddingDimensions))
{ throw new InvalidOperationException("Elasticsearch requires a direct endpoint and dimensions matching the embedding provider."); }
static bool DirectEndpoint(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);


app.UseExceptionHandler();
app.UseStatusCodePages();

var configuredApiKey = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value.ApiKey;
if (string.IsNullOrEmpty(configuredApiKey))
{
    app.Logger.LogWarning("No RAG_API_KEY is configured; the API is accepting unauthenticated requests.");
}
else
{
    var apiKey = configuredApiKey;
    app.Use(async (context, next) =>
    {
        if (ApiKeyAuthorization.IsExempt(context.Request.Path) || ApiKeyAuthorization.IsAuthorized(context.Request.Headers, apiKey))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        await Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "A valid X-API-Key header is required.").ExecuteAsync(context).ConfigureAwait(false);
    });
}

app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "Enterprise RAG API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Enterprise RAG API v1");
});

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "rag-api" }))
    .WithName("HealthCheck")
    .WithTags("Health");

app.MapPost("/documents", async (ApiIngestionRequest request, IIngestionJobQueue queue, CancellationToken cancellationToken) =>
{
    var sources = request.GetSources();
    if (sources.Count == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["sources"] = ["Provide at least one source URI or path."]
        });
    }

    var job = await queue.EnqueueAsync(new IngestionRequest(Strategy: request.Strategy, Sources: sources), cancellationToken).ConfigureAwait(false);
    return Results.Accepted($"/jobs/{job.Id}", new { jobId = job.Id, status = job.Status.ToString() });
})
    .WithName("EnqueueDocuments")
    .WithTags("Documents");

app.MapGet("/jobs/{id}", async (string id, IIngestionJobStore store, CancellationToken cancellationToken) =>
{
    var job = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    return job is null ? Results.NotFound() : Results.Ok(job.ToStatusResponse());
})
    .WithName("GetJob")
    .WithTags("Jobs");

app.MapPost("/jobs/{id}/pause", async Task<IResult> (string id, IIngestionJobStore store, CancellationToken cancellationToken) =>
{
    var existing = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    if (existing is null)
    {
        return Results.NotFound();
    }

    var job = await store.MarkPausedAsync(id, cancellationToken).ConfigureAwait(false);
    return Results.Ok((job ?? existing).ToStatusResponse());
})
    .WithName("PauseJob")
    .WithTags("Jobs");

app.MapPost("/jobs/{id}/cancel", async Task<IResult> (string id, IIngestionJobStore store, CancellationToken cancellationToken) =>
{
    var existing = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    if (existing is null)
    {
        return Results.NotFound();
    }

    var job = await store.MarkCanceledAsync(id, cancellationToken).ConfigureAwait(false);
    return Results.Ok((job ?? existing).ToStatusResponse());
})
    .WithName("CancelJob")
    .WithTags("Jobs");

app.MapPost("/jobs/{id}/resume", async Task<IResult> (string id, IIngestionJobStore store, IIngestionJobQueue queue, CancellationToken cancellationToken) =>
{
    var existing = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    if (existing is null)
    {
        return Results.NotFound();
    }

    if (existing.Status != IngestionJobStatus.Paused)
    {
        return Results.Ok(existing.ToStatusResponse());
    }

    var job = await store.MarkQueuedAsync(id, cancellationToken).ConfigureAwait(false);
    if (job is not null && job.Status == IngestionJobStatus.Queued)
    {
        await queue.EnqueueExistingAsync(job, cancellationToken).ConfigureAwait(false);
    }

    return Results.Ok((job ?? existing).ToStatusResponse());
})
    .WithName("ResumeJob")
    .WithTags("Jobs");

app.MapPost("/chunk/preview", async Task<IResult> (ChunkPreviewRequest request, IChunkPreviewService previewService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Path))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["path"] = ["Provide a document path or source URI."]
        });
    }

    var result = await previewService.PreviewAsync(request.Path.Trim(), cancellationToken).ConfigureAwait(false);
    return Results.Ok(result);
})
    .WithName("PreviewChunks")
    .WithTags("Chunks");

app.MapPost("/query", async Task<IResult> (ApiQueryRequest request, IQueryPipeline pipeline, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["question"] = ["Provide a question."]
        });
    }

    if (request.TopK is < 1 or > ApiLimits.MaxTopK)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["topK"] = [$"topK must be between 1 and {ApiLimits.MaxTopK}."]
        });
    }

    var answer = await pipeline.QueryAsync(new QueryRequest(request.Question, request.TopK, request.Filter?.ToCoreFilter()), cancellationToken).ConfigureAwait(false);
    return Results.Ok(answer);
})
    .WithName("Query")
    .WithTags("Query");

app.MapRagWorkbench();

app.Run();

internal static class ApiLimits
{
    public const int MaxTopK = 100;
}

/// <summary>
/// Opt-in API key check used when <see cref="ApiOptions.ApiKey"/> is configured. Kept as a small,
/// dependency-free static helper (no ASP.NET Core Identity, no new package) so the comparison logic
/// stays simple and easy to reason about.
/// </summary>
internal static class ApiKeyAuthorization
{
    private const string HeaderName = "X-API-Key";

    /// <summary>
    /// Routes that stay reachable without an API key: health checks, the root redirect, and Swagger.
    /// </summary>
    public static bool IsExempt(PathString path)
    {
        return !path.HasValue
            || path.Equals("/", StringComparison.Ordinal)
            || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAuthorized(IHeaderDictionary headers, string expectedApiKey)
    {
        return headers.TryGetValue(HeaderName, out var provided) && Matches(provided.ToString(), expectedApiKey);
    }

    /// <summary>
    /// Constant-time comparison over UTF-8 bytes so a caller cannot infer the key by timing how long a
    /// near-miss took to reject. Lengths are compared first because <see cref="CryptographicOperations.FixedTimeEquals"/>
    /// requires equal-length spans.
    /// </summary>
    internal static bool Matches(string provided, string expectedApiKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedApiKey);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}

/// <summary>
/// Maps platform exceptions to problem responses. Exception messages are omitted from responses and logs:
/// they embed resolved absolute paths, store connection strings, and other host detail that an
/// unauthenticated caller must not learn. Correlate a response with its log entry by trace id.
/// </summary>
internal sealed class RagExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<RagExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, detail) = exception switch
        {
            IngestionCapacityException => (StatusCodes.Status429TooManyRequests, "Ingestion capacity reached. Retry later."),
            SourcePathNotAllowedException => (StatusCodes.Status403Forbidden, "The requested path is outside the allowed ingestion roots."),
            FileNotFoundException or DirectoryNotFoundException => (StatusCodes.Status404NotFound, "The requested document was not found."),
            NotSupportedException => (StatusCodes.Status400BadRequest, "The requested document type is not supported."),
            InvalidDataException => (StatusCodes.Status400BadRequest, "The document could not be parsed. Check the file and its ingestion schema."),
            _ => (StatusCodes.Status500InternalServerError, "The request could not be completed.")
        };

        logger.LogError(
            "{Method} {Path} failed with status {Status} ({ErrorType}).",
            httpContext.Request.Method,
            httpContext.Request.Path,
            status, exception.GetType().Name);

        if (status == 429) { httpContext.Response.Headers.RetryAfter = "60"; }
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Detail = detail
            }
        }).ConfigureAwait(false);
    }
}

internal static class IngestionJobApiExtensions
{
    public static object ToStatusResponse(this IngestionJob job)
    {
        return new
        {
            jobId = job.Id,
            status = job.Status.ToString(),
            sources = job.Request.SourceUris,
            strategy = job.Request.Strategy,
            documentCount = job.DocumentCount,
            chunkCount = job.ChunkCount,
            totalSourceCount = job.TotalSourceCount,
            processedSourceCount = job.ProcessedSourceCount,
            currentSource = job.CurrentSource,
            workerId = job.WorkerId,
            error = job.Error is null ? null : "Ingestion failed. Check server diagnostics.",
            createdAt = job.CreatedAt,
            updatedAt = job.UpdatedAt,
            startedAt = job.StartedAt,
            completedAt = job.CompletedAt
        };
    }
}
