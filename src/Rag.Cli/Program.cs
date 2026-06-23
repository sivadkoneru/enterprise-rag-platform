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

var ingest = new Command("ingest", "Ingest a document or directory.");
var ingestPath = new Argument<string>("path", "File or directory to ingest.");
var strategy = new Option<string?>("--strategy", "Chunking strategy override.");
ingest.AddArgument(ingestPath);
ingest.AddOption(strategy);
ingest.SetHandler(async (string path, string? selectedStrategy) =>
{
    await RunSafelyAsync(() => IngestAsync(path, selectedStrategy, services)).ConfigureAwait(false);
}, ingestPath, strategy);

var preview = new Command("chunk:preview", "Preview all chunking strategies for a document.");
var previewPath = new Argument<string>("path", "Document path to preview.");
preview.AddArgument(previewPath);
preview.SetHandler(async (string path) =>
{
    await RunSafelyAsync(() => PreviewAsync(path, services)).ConfigureAwait(false);
}, previewPath);

var query = new Command("query", "Ask a grounded question over indexed documents.");
var question = new Argument<string[]>("question", "Question text.") { Arity = ArgumentArity.OneOrMore };
query.AddArgument(question);
query.SetHandler(async (string[] parts) =>
{
    await RunSafelyAsync(() => QueryAsync(string.Join(' ', parts), services)).ConfigureAwait(false);
}, question);

var config = new Command("config", "Print effective provider selections without secrets.");
config.SetHandler(() => PrintConfig(configuration));

root.AddCommand(ingest);
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

static async Task IngestAsync(string path, string? strategy, IServiceProvider services)
{
    var pipeline = services.GetRequiredService<IIngestionPipeline>();
    var result = await pipeline.IngestAsync(new IngestionRequest(path, strategy)).ConfigureAwait(false);
    Console.WriteLine($"document={result.DocumentId} strategy={result.Strategy} chunks={result.ChunkCount}");
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

static async Task QueryAsync(string question, IServiceProvider services)
{
    var pipeline = services.GetRequiredService<IQueryPipeline>();
    var answer = await pipeline.QueryAsync(new QueryRequest(question)).ConfigureAwait(false);
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
