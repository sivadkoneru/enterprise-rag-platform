using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Rag.Api.Contracts;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
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

// Opt-in provider packages: registered here so every documented DOC_STORE / JOB_STORE / source
// scheme (mongo, cosmos, s3, azureblob) keeps working exactly as it does today. A deployment that
// wants a smaller dependency graph can drop the reference and the matching AddRag* call instead.
builder.Services.AddRagAwsS3(builder.Configuration);
builder.Services.AddRagAzureBlob(builder.Configuration);
builder.Services.AddRagCosmos(builder.Configuration);
builder.Services.AddRagMongo(builder.Configuration);

builder.Services.AddRagIngestionWorker();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RagExceptionHandler>();

var app = builder.Build();

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
/// Maps platform exceptions to problem responses. Exception messages are logged, never returned:
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
            SourcePathNotAllowedException => (StatusCodes.Status403Forbidden, "The requested path is outside the allowed ingestion roots."),
            FileNotFoundException or DirectoryNotFoundException => (StatusCodes.Status404NotFound, "The requested document was not found."),
            NotSupportedException => (StatusCodes.Status400BadRequest, "The requested document type is not supported."),
            InvalidDataException => (StatusCodes.Status400BadRequest, "The document could not be parsed. Check the file and its ingestion schema."),
            _ => (StatusCodes.Status500InternalServerError, "The request could not be completed.")
        };

        logger.LogError(
            exception,
            "{Method} {Path} failed with status {Status}.",
            httpContext.Request.Method,
            httpContext.Request.Path,
            status);

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
            error = job.Error,
            createdAt = job.CreatedAt,
            updatedAt = job.UpdatedAt,
            startedAt = job.StartedAt,
            completedAt = job.CompletedAt
        };
    }
}
