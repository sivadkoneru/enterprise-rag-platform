using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using System.CommandLine;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(EnvFile.LoadFromWorkingDirectory())
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection()
    .AddRagPlatform(configuration)
    .BuildServiceProvider();

var root = new RootCommand("Enterprise RAG Platform CLI");

var ingest = new Command("ingest", "Ingest one or more source URIs or paths.");
var ingestUris = new Argument<string[]>("uri", "Source URI or path to ingest.") { Arity = ArgumentArity.OneOrMore };
var strategy = new Option<string?>("--strategy", "Chunking strategy override.");
var wait = new Option<bool>("--wait", "Wait for the inline ingestion job to complete.");
ingest.AddArgument(ingestUris);
ingest.AddOption(strategy);
ingest.AddOption(wait);
ingest.SetHandler(async (string[] uris, string? selectedStrategy, bool shouldWait) =>
{
    await RunSafelyAsync(() => IngestAsync(uris, selectedStrategy, shouldWait, services)).ConfigureAwait(false);
}, ingestUris, strategy, wait);

var jobs = new Command("jobs", "Inspect ingestion jobs.");
var jobStatus = new Command("status", "Show ingestion job status.");
var jobId = new Argument<string>("id", "Ingestion job id.");
jobStatus.AddArgument(jobId);
jobStatus.SetHandler((string id) => PrintJobStatus(id, services), jobId);
jobs.AddCommand(jobStatus);

var preview = new Command("chunk:preview", "Preview all chunking strategies for a document.");
var previewPath = new Argument<string>("path", "Document path to preview.");
preview.AddArgument(previewPath);
preview.SetHandler(async (string path) =>
{
    await RunSafelyAsync(() => PreviewAsync(path, services)).ConfigureAwait(false);
}, previewPath);

var query = new Command("query", "Ask a grounded question over indexed documents.");
var question = new Argument<string[]>("question", "Question text.") { Arity = ArgumentArity.OneOrMore };
var source = new Option<string[]>("--source", () => Array.Empty<string>(), "Filter by source or origin.") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
var document = new Option<string[]>("--document", () => Array.Empty<string>(), "Filter by document id.") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
var type = new Option<string[]>("--type", () => Array.Empty<string>(), "Filter by file type or extension.") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
query.AddArgument(question);
query.AddOption(source);
query.AddOption(document);
query.AddOption(type);
query.SetHandler(async (string[] parts, string[] sources, string[] documents, string[] types) =>
{
    await RunSafelyAsync(() => QueryAsync(string.Join(' ', parts), new CliQueryFilters(sources, documents, types), services)).ConfigureAwait(false);
}, question, source, document, type);

var config = new Command("config", "Print effective provider selections without secrets.");
config.SetHandler(() => PrintConfig(configuration));

root.AddCommand(ingest);
root.AddCommand(jobs);
root.AddCommand(preview);
root.AddCommand(query);
root.AddCommand(config);

return await root.InvokeAsync(args).ConfigureAwait(false);

static async Task RunSafelyAsync(Func<Task> action)
{
    try
    {
        await action().ConfigureAwait(false);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 2;
    }
}

static async Task IngestAsync(string[] uris, string? strategy, bool wait, IServiceProvider services)
{
    var jobStore = services.GetRequiredService<IIngestionJobStore>();
    var job = await jobStore.CreateAsync(new IngestionRequest(Strategy: strategy, Sources: uris)).ConfigureAwait(false);
    Console.WriteLine($"jobId={job.Id} status={job.Status} mode={(wait ? "wait" : "inline")}");

    var pipeline = services.GetRequiredService<IIngestionPipeline>();
    await jobStore.MarkRunningAsync(job.Id).ConfigureAwait(false);

    try
    {
        var result = await pipeline.IngestAsync(new IngestionRequest(Strategy: strategy, Sources: uris)).ConfigureAwait(false);
        await jobStore.MarkSucceededAsync(job.Id, result).ConfigureAwait(false);

        var completed = await jobStore.GetAsync(job.Id).ConfigureAwait(false);
        Console.WriteLine($"jobId={job.Id} status={completed?.Status.ToString() ?? "Succeeded"} documents={completed?.DocumentCount ?? 0} chunks={completed?.ChunkCount ?? result.ChunkCount}");
    }
    catch (Exception exception)
    {
        await jobStore.MarkFailedAsync(job.Id, exception.Message).ConfigureAwait(false);
        Console.Error.WriteLine($"jobId={job.Id} status=Failed error={exception.Message}");
        throw;
    }
}

static async Task PreviewAsync(string path, IServiceProvider services)
{
    var preview = services.GetRequiredService<IChunkPreviewService>();
    var rows = await preview.PreviewAsync(path).ConfigureAwait(false);
    Console.WriteLine("strategy\tchunks\tavg_size\toverlap\tsample");
    foreach (var row in rows)
    {
        Console.WriteLine($"{row.Strategy}\t{row.ChunkCount}\t{row.AverageSize:F1}\t{row.Overlap}\t{row.Sample.Replace('\n', ' ')}");
    }
}

static async Task QueryAsync(string question, CliQueryFilters filters, IServiceProvider services)
{
    var pipeline = services.GetRequiredService<IQueryPipeline>();
    var answer = await pipeline.QueryAsync(new QueryRequest(question, Filter: filters.ToCoreFilter())).ConfigureAwait(false);
    Console.WriteLine(answer.Answer);
    foreach (var citation in answer.Citations)
    {
        Console.WriteLine($"- {citation.Source}#{citation.ChunkIndex} score={citation.Score:F3}");
    }
}

static void PrintConfig(IConfiguration configuration)
{
    Console.WriteLine($"LLM_PROVIDER={configuration["LLM_PROVIDER"] ?? configuration["Llm:Provider"] ?? "deterministic"}");
    Console.WriteLine($"DOC_STORE={configuration["DOC_STORE"] ?? configuration["DocumentStore:Provider"] ?? "memory"}");
    Console.WriteLine($"VECTOR_STORE={configuration["VECTOR_STORE"] ?? configuration["VectorStore:Provider"] ?? "memory"}");
    Console.WriteLine($"CHUNKING_STRATEGY={configuration["CHUNKING_STRATEGY"] ?? configuration["Rag:ChunkingStrategy"] ?? "fixed"}");
}

static void PrintJobStatus(string id, IServiceProvider services)
{
    var jobs = services.GetRequiredService<IIngestionJobStore>();
    var job = jobs.GetAsync(id).GetAwaiter().GetResult();
    if (job is null)
    {
        Console.WriteLine($"jobId={id} status=Unknown");
        Console.WriteLine("CLI jobs are in-memory for the current process.");
        return;
    }

    Console.WriteLine($"jobId={job.Id} status={job.Status} documents={job.DocumentCount} chunks={job.ChunkCount} error={job.Error ?? string.Empty}");
}

internal sealed record CliQueryFilters(IReadOnlyList<string>? Sources, IReadOnlyList<string>? Documents, IReadOnlyList<string>? Types)
{
    public VectorSearchFilter? ToCoreFilter()
    {
        var sources = Sources ?? [];
        var documents = Documents ?? [];
        var types = Types ?? [];

        if (sources.Count == 0 && documents.Count == 0 && types.Count == 0)
        {
            return null;
        }

        return new VectorSearchFilter(
            DocumentIds: documents.Count == 0 ? null : documents,
            Sources: sources.Count == 0 ? null : sources,
            Origins: sources.Count == 0 ? null : sources,
            FileTypes: types.Count == 0 ? null : types);
    }
}
