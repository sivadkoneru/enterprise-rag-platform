using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(EnvFile.LoadFromWorkingDirectory())
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection()
    .AddRagPlatform(configuration)
    .BuildServiceProvider();

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    return args[0] switch
    {
        "ingest" => await IngestAsync(args, services).ConfigureAwait(false),
        "chunk:preview" => await PreviewAsync(args, services).ConfigureAwait(false),
        "query" => await QueryAsync(args, services).ConfigureAwait(false),
        "config" => PrintConfig(configuration),
        _ => Unknown(args[0])
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

static async Task<int> IngestAsync(string[] args, IServiceProvider services)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("ingest requires a file or directory path.");
        return 1;
    }

    var pipeline = services.GetRequiredService<IIngestionPipeline>();
    var result = await pipeline.IngestAsync(new IngestionRequest(args[1], StrategyFromArgs(args))).ConfigureAwait(false);
    Console.WriteLine($"document={result.DocumentId} strategy={result.Strategy} chunks={result.ChunkCount}");
    return 0;
}

static async Task<int> PreviewAsync(string[] args, IServiceProvider services)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("chunk:preview requires a document path.");
        return 1;
    }

    var preview = services.GetRequiredService<IChunkPreviewService>();
    var rows = await preview.PreviewAsync(args[1]).ConfigureAwait(false);
    Console.WriteLine("strategy\tchunks\tavg_size\toverlap\tsample");
    foreach (var row in rows)
    {
        Console.WriteLine($"{row.Strategy}\t{row.ChunkCount}\t{row.AverageSize:F1}\t{row.Overlap}\t{row.Sample.Replace('\n', ' ')}");
    }

    return 0;
}

static async Task<int> QueryAsync(string[] args, IServiceProvider services)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("query requires a question.");
        return 1;
    }

    var pipeline = services.GetRequiredService<IQueryPipeline>();
    var answer = await pipeline.QueryAsync(new QueryRequest(string.Join(' ', args.Skip(1)))).ConfigureAwait(false);
    Console.WriteLine(answer.Answer);
    foreach (var citation in answer.Citations)
    {
        Console.WriteLine($"- {citation.Source}#{citation.ChunkIndex} score={citation.Score:F3}");
    }

    return 0;
}

static int PrintConfig(IConfiguration configuration)
{
    Console.WriteLine($"LLM_PROVIDER={configuration["LLM_PROVIDER"] ?? configuration["Llm:Provider"] ?? "deterministic"}");
    Console.WriteLine($"DOC_STORE={configuration["DOC_STORE"] ?? configuration["DocumentStore:Provider"] ?? "memory"}");
    Console.WriteLine($"VECTOR_STORE={configuration["VECTOR_STORE"] ?? configuration["VectorStore:Provider"] ?? "memory"}");
    Console.WriteLine($"CHUNKING_STRATEGY={configuration["CHUNKING_STRATEGY"] ?? configuration["Rag:ChunkingStrategy"] ?? "fixed"}");
    return 0;
}

static string? StrategyFromArgs(string[] args)
{
    var strategy = args.FirstOrDefault(arg => arg.StartsWith("--strategy=", StringComparison.OrdinalIgnoreCase));
    return strategy?.Split('=', 2)[1];
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  rag ingest <path> [--strategy=fixed|recursive|markdown|semantic]");
    Console.WriteLine("  rag chunk:preview <path>");
    Console.WriteLine("  rag query <question>");
    Console.WriteLine("  rag config");
}
