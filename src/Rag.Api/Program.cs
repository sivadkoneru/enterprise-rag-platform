using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration
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
builder.Services.AddSwaggerGen();
builder.Services.AddRagPlatform(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "rag-api" }));

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
});

app.MapGet("/jobs/{id}", async (string id, IIngestionJobStore store, CancellationToken cancellationToken) =>
{
    var job = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    return job is null ? Results.NotFound() : Results.Ok(job.ToStatusResponse());
});

app.MapPost("/chunk/preview", async (ChunkPreviewRequest request, IChunkPreviewService previewService, CancellationToken cancellationToken) =>
{
    var result = await previewService.PreviewAsync(request.Path, cancellationToken).ConfigureAwait(false);
    return Results.Ok(result);
});

app.MapPost("/query", async (ApiQueryRequest request, IQueryPipeline pipeline, CancellationToken cancellationToken) =>
{
    var answer = await pipeline.QueryAsync(new QueryRequest(request.Question, request.TopK, request.Filter?.ToCoreFilter()), cancellationToken).ConfigureAwait(false);
    return Results.Ok(answer);
});

app.Run();

public sealed record ChunkPreviewRequest(string Path);

public sealed record ApiIngestionRequest(string? Path = null, string? Strategy = null, IReadOnlyList<string>? Sources = null)
{
    public IReadOnlyList<string> GetSources()
    {
        if (Sources is { Count: > 0 })
        {
            return Sources.Where(source => !string.IsNullOrWhiteSpace(source)).Select(source => source.Trim()).ToArray();
        }

        return string.IsNullOrWhiteSpace(Path) ? [] : [Path.Trim()];
    }
}

public sealed record ApiQueryRequest(string Question, int TopK = 5, ApiQueryFilter? Filter = null);

public sealed record ApiQueryFilter(
    IReadOnlyList<string>? Sources = null,
    IReadOnlyList<string>? DocumentIds = null,
    IReadOnlyList<string>? Documents = null,
    IReadOnlyList<string>? Origins = null,
    IReadOnlyList<string>? FileTypes = null,
    IReadOnlyList<string>? Types = null)
{
    public VectorSearchFilter ToCoreFilter()
    {
        return new VectorSearchFilter(
            DocumentIds: Merge(DocumentIds, Documents),
            Sources: Sources,
            Origins: Origins,
            FileTypes: Merge(FileTypes, Types));
    }

    private static IReadOnlyList<string>? Merge(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
    {
        var values = new List<string>();
        if (first is { Count: > 0 })
        {
            values.AddRange(first);
        }

        if (second is { Count: > 0 })
        {
            values.AddRange(second);
        }

        return values.Count == 0 ? null : values;
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
            documentIds = job.DocumentIds ?? [],
            chunkIds = job.ChunkIds ?? [],
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
