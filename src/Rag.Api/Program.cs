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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRagPlatform(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "rag-api" }));

app.MapPost("/documents", async (IngestionRequest request, IIngestionPipeline pipeline, CancellationToken cancellationToken) =>
{
    var result = await pipeline.IngestAsync(request, cancellationToken).ConfigureAwait(false);
    return Results.Accepted($"/documents/{result.DocumentId}", result);
});

app.MapPost("/chunk/preview", async (ChunkPreviewRequest request, IChunkPreviewService previewService, CancellationToken cancellationToken) =>
{
    var result = await previewService.PreviewAsync(request.Path, cancellationToken).ConfigureAwait(false);
    return Results.Ok(result);
});

app.MapPost("/query", async (QueryRequest request, IQueryPipeline pipeline, CancellationToken cancellationToken) =>
{
    var answer = await pipeline.QueryAsync(request, cancellationToken).ConfigureAwait(false);
    return Results.Ok(answer);
});

app.Run();

public sealed record ChunkPreviewRequest(string Path);
