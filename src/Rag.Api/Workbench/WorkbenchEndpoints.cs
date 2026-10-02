using System.Text.Json;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Workbench;

namespace Rag.Api.Workbench;

public static class WorkbenchEndpoints
{
    private static readonly string[] SupportedStrategies = ["fixed", "recursive", "markdown-aware", "semantic"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static WebApplication MapRagWorkbench(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Live RAG Workbench");
        api.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context).ConfigureAwait(false); }
            catch (ArgumentException exception) { return Results.Problem(statusCode: 400, title: "Invalid request", detail: exception.Message); }
            catch (KeyNotFoundException) { return Results.Problem(statusCode: 404, title: "Not found"); }
        });
        api.MapGet("/capabilities", (IOptions<LlmOptions> llm, IOptions<DocumentStoreOptions> documents, IOptions<VectorStoreOptions> vectors, IOptions<JobStoreOptions> jobs, IOptions<RerankerOptions> reranker, IOptions<WorkbenchQueryOptions> queryDefaults) => Results.Ok(new
        {
            schemaVersion = 1, reranker = !string.IsNullOrWhiteSpace(reranker.Value.Endpoint), hybrid = true,
            embeddingModel = WorkbenchModelIdentity.EmbeddingModel(llm.Value), embeddingDimensions = llm.Value.EmbeddingDimensions,
            documentStore = documents.Value.Provider, vectorStore = vectors.Value.Provider, jobStore = jobs.Value.Provider,
            workbenchJobStore = jobs.Value.Provider, defaultQuery = queryDefaults.Value,
            vectorScoreKind = "normalized-cosine", lexicalAlgorithm = "BM25", tokenUsageKind = "estimated-chars/4", llmProvider = llm.Value.Provider,
            supportedStrategies = SupportedStrategies
        }));
        api.MapPost("/checks/{kind}", async (string kind, WorkbenchEnvironment environment, CancellationToken token) => Results.Ok(await environment.CheckProviderAsync(kind, token).ConfigureAwait(false)));
        api.MapGet("/configuration", async (WorkbenchEnvironment environment, CancellationToken token) => Results.Ok(await environment.ConfigurationAsync(token).ConfigureAwait(false)));
        api.MapGet("/readiness", async (WorkbenchEnvironment environment, CancellationToken token) => Results.Ok(await environment.ReadinessAsync(token).ConfigureAwait(false)));
        api.MapGet("/corpora", async (WorkbenchCatalog catalog, CancellationToken token) => Results.Ok(await catalog.ListCorporaAsync(token).ConfigureAwait(false)));
        api.MapPost("/corpora", async (CreateCorpusRequest request, WorkbenchCatalog catalog, CancellationToken token) =>
        {
            var corpus = await catalog.CreateCorpusAsync(request, token).ConfigureAwait(false);
            return Results.Created($"/api/v1/corpora/{corpus.Id}", corpus);
        });
        api.MapGet("/corpora/{corpusId}/profiles", async (string corpusId, WorkbenchCatalog catalog, CancellationToken token) => Results.Ok(await catalog.ListProfilesAsync(corpusId, token).ConfigureAwait(false)));
        api.MapPost("/corpora/{corpusId}/profiles", async (string corpusId, CreateProfileRequest request, WorkbenchCatalog catalog, CancellationToken token) =>
        {
            var profile = await catalog.CreateProfileAsync(corpusId, request, token).ConfigureAwait(false);
            return Results.Created($"/api/v1/corpora/{corpusId}/profiles/{profile.Id}", profile);
        });
        api.MapPost("/profiles/{profileId}/ingestions", async (string profileId, WorkbenchIngestionRequest request, WorkbenchJobs jobs, CancellationToken token) =>
        {
            var job = await jobs.EnqueueIngestionAsync(profileId, request, token).ConfigureAwait(false);
            return Results.Accepted($"/api/v1/jobs/{job.Id}", PublicJob(job));
        });
        api.MapGet("/jobs", async (string? profileId, WorkbenchJobs jobs, CancellationToken token) => Results.Ok((await jobs.ListAsync(profileId, token).ConfigureAwait(false)).Select(PublicJob)));
        api.MapGet("/jobs/{id}", async (string id, WorkbenchJobs jobs, CancellationToken token) =>
        {
            var job = await jobs.GetAsync(id, token).ConfigureAwait(false);
            return job is null ? Results.NotFound() : Results.Ok(PublicJob(job));
        });
        foreach (var action in new[] { "pause", "resume", "cancel" })
        {
            var capturedAction = action;
            api.MapPost($"/jobs/{{id}}/{action}", async (string id, WorkbenchJobs jobs, CancellationToken token) => Results.Ok(PublicJob(await jobs.ControlAsync(id, capturedAction, token).ConfigureAwait(false))));
        }
        api.MapGet("/profiles/{profileId}/documents", async (string profileId, int? offset, int? limit, WorkbenchCatalog catalog, CancellationToken token) => Results.Ok(WorkbenchCatalog.Page(await catalog.GetDocumentsAsync(profileId, token).ConfigureAwait(false), offset ?? 0, limit ?? 50)));
        api.MapGet("/profiles/{profileId}/documents/{documentId}", async (string profileId, string documentId, WorkbenchCatalog catalog, CancellationToken token) =>
        {
            var document = (await catalog.GetDocumentsAsync(profileId, token).ConfigureAwait(false)).FirstOrDefault(document => document.Id == documentId);
            return document is null ? Results.NotFound() : Results.Ok(document);
        });
        api.MapGet("/profiles/{profileId}/chunks", async (string profileId, string? documentId, int? offset, int? limit, WorkbenchCatalog catalog, CancellationToken token) =>
        {
            var chunks = await catalog.GetChunksAsync(profileId, documentId, token).ConfigureAwait(false);
            var response = chunks.Select(chunk => new { id = chunk.Id, documentId = chunk.DocumentId, filename = chunk.Metadata.FileName, index = chunk.Index, content = chunk.Text, startOffset = chunk.StartOffset, endOffset = chunk.EndOffset }).ToArray();
            return Results.Ok(WorkbenchCatalog.Page(response, offset ?? 0, limit ?? 50));
        });
        api.MapPost("/queries", async (ApiDetailedQueryRequest input, IOptions<WorkbenchQueryOptions> defaults, DetailedQueryPipeline pipeline, HttpContext context, ILoggerFactory loggers, CancellationToken token) =>
        {
            var request = input.ToCore(defaults.Value);
            DetailedQueryPipeline.Validate(request);
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache, no-transform";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            async Task EmitAsync(string kind, object payload, CancellationToken cancellation)
            {
                await context.Response.WriteAsync($"event: {kind}\ndata: {JsonSerializer.Serialize(payload, Json)}\n\n", cancellation).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(cancellation).ConfigureAwait(false);
            }
            try
            {
                var result = await pipeline.QueryAsync(request, (stage, cancellation) => EmitAsync("stage", stage, cancellation), token).ConfigureAwait(false);
                await EmitAsync("result", result, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { loggers.CreateLogger("WorkbenchQueries").LogInformation("Query request {TraceId} was canceled.", context.TraceIdentifier); }
            catch (Exception exception)
            {
                loggers.CreateLogger("WorkbenchQueries").LogError("Query request {TraceId} failed ({ErrorType}).", context.TraceIdentifier, exception.GetType().Name);
                if (!token.IsCancellationRequested) { await EmitAsync("error", new { message = "Query failed. Verify the selected profile and provider readiness.", traceId = context.TraceIdentifier }, token).ConfigureAwait(false); }
            }
        });
        api.MapPost("/evaluations", async (ApiLiveEvaluationRequest request, IOptions<WorkbenchQueryOptions> defaults, WorkbenchJobs jobs, CancellationToken token) =>
        {
            var job = await jobs.EnqueueEvaluationAsync(request.ToCore(defaults.Value), token).ConfigureAwait(false);
            return Results.Accepted($"/api/v1/evaluations/{job.Id}", PublicJob(job));
        });
        api.MapGet("/evaluations/{id}", async (string id, WorkbenchCatalog catalog, CancellationToken token) =>
        {
            var report = await catalog.Store.GetAsync<LiveEvaluationReport>("evaluations", id, token).ConfigureAwait(false);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });
        api.MapGet("/evaluations/{id}/export", async (string id, WorkbenchCatalog catalog, CancellationToken token) =>
        {
            var report = await catalog.Store.GetAsync<LiveEvaluationReport>("evaluations", id, token).ConfigureAwait(false);
            return report is null ? Results.NotFound() : Results.File(JsonSerializer.SerializeToUtf8Bytes(report, Json), "application/json", $"rag-evaluation-{report.Id}.json");
        });
        return app;
    }
    private static object PublicJob(WorkbenchJob job) => new { job.Id, job.Kind, job.Status, job.ProfileId, job.Completed, job.Total, job.Error, job.CreatedAt, job.UpdatedAt };
}
