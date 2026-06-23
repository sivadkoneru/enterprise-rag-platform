using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Rag.Cli;

/// <summary>
/// Builds and runs the CLI command tree. Kept separate from <c>Program.cs</c> so the command
/// wiring can be driven from tests without going through the process entry point.
/// </summary>
internal static class CliApplication
{
    public static async Task<int> RunAsync(
        string[] args,
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var root = BuildRootCommand(services, configuration, cancellationToken);
        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    private static RootCommand BuildRootCommand(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var root = new RootCommand("Enterprise RAG Platform CLI");

        var ingest = new Command("ingest", "Ingest one or more source URIs or paths.");
        var ingestUris = new Argument<string[]>("uri", "Source URI or path to ingest.") { Arity = ArgumentArity.OneOrMore };

        // Without this the multi-valued argument silently swallows a mistyped or retired option and
        // reports it as a missing path.
        ingestUris.AddValidator(result =>
        {
            var unrecognized = result.Tokens
                .Select(token => token.Value)
                .Where(value => value.StartsWith("--", StringComparison.Ordinal))
                .ToArray();
            if (unrecognized.Length > 0)
            {
                result.ErrorMessage = $"Unrecognized option(s): {string.Join(", ", unrecognized)}.";
            }
        });
        var strategy = new Option<string?>("--strategy", "Chunking strategy override.");
        ingest.AddArgument(ingestUris);
        ingest.AddOption(strategy);
        ingest.SetHandler((InvocationContext context) => RunSafelyAsync(
            context,
            () => IngestAsync(
                context.ParseResult.GetValueForArgument(ingestUris),
                context.ParseResult.GetValueForOption(strategy),
                services,
                cancellationToken)));

        var jobs = new Command("jobs", "Inspect ingestion jobs.");
        var jobStatus = new Command("status", "Show ingestion job status.");
        var jobId = new Argument<string>("id", "Ingestion job id.");
        jobStatus.AddArgument(jobId);
        jobStatus.SetHandler((InvocationContext context) => RunSafelyAsync(
            context,
            () => PrintJobStatusAsync(context.ParseResult.GetValueForArgument(jobId), services, cancellationToken)));
        jobs.AddCommand(jobStatus);

        var preview = new Command("chunk:preview", "Preview all chunking strategies for a document.");
        var previewPath = new Argument<string>("path", "Document path to preview.");
        preview.AddArgument(previewPath);
        preview.SetHandler((InvocationContext context) => RunSafelyAsync(
            context,
            () => PreviewAsync(context.ParseResult.GetValueForArgument(previewPath), services, cancellationToken)));

        var query = new Command("query", "Ask a grounded question over indexed documents.");
        var question = new Argument<string[]>("question", "Question text.") { Arity = ArgumentArity.OneOrMore };
        var source = new Option<string[]>("--source", () => Array.Empty<string>(), "Filter by exact source URI or path.") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var origin = new Option<string[]>("--origin", () => Array.Empty<string>(), "Filter by source origin: file, s3, or azureblob.") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var document = new Option<string[]>("--document", () => Array.Empty<string>(), "Filter by document id.") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var type = new Option<string[]>("--type", () => Array.Empty<string>(), "Filter by file type or extension.") { Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        query.AddArgument(question);
        query.AddOption(source);
        query.AddOption(origin);
        query.AddOption(document);
        query.AddOption(type);
        query.SetHandler((InvocationContext context) => RunSafelyAsync(
            context,
            () => QueryAsync(
                string.Join(' ', context.ParseResult.GetValueForArgument(question)),
                // Each flag maps to exactly one VectorSearchFilter field, matching the API. The
                // shared VectorSearchFilter.FromLists factory does the normalization (empty/null
                // lists collapse to null per field, and an all-empty filter collapses to no filter
                // at all), so this stays identical to the normalization the API applies.
                VectorSearchFilter.FromLists(
                    documentIds: context.ParseResult.GetValueForOption(document),
                    sources: context.ParseResult.GetValueForOption(source),
                    origins: context.ParseResult.GetValueForOption(origin),
                    fileTypes: context.ParseResult.GetValueForOption(type)),
                services,
                cancellationToken)));

        var config = new Command("config", "Print effective provider selections without secrets.");
        config.SetHandler(() => PrintConfig(configuration));

        root.AddCommand(ingest);
        root.AddCommand(jobs);
        root.AddCommand(preview);
        root.AddCommand(query);
        root.AddCommand(config);

        return root;
    }

    // System.CommandLine sets InvocationContext.ExitCode (not Environment.ExitCode) as the value
    // returned from InvokeAsync, so handlers must report failure through the context or the process
    // always exits 0 regardless of what the handler did.
    private static async Task RunSafelyAsync(InvocationContext context, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            context.ExitCode = 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            context.ExitCode = 2;
        }
    }

    // The CLI runs ingestion inline (create -> mark-running -> ingest -> mark-succeeded/failed)
    // rather than through IIngestionJobQueue/IngestionBackgroundService: the CLI process builds a
    // plain ServiceCollection with no IHost, so the queued background worker (opt-in via
    // AddRagIngestionWorker, and only registered by the API) would never run here. A short-lived
    // CLI invocation that returns when the ingestion finishes is a legitimate synchronous mode in
    // its own right, so this difference from the API's async job flow is intentional, not accidental.
    private static async Task IngestAsync(string[] uris, string? strategy, IServiceProvider services, CancellationToken cancellationToken)
    {
        var request = new IngestionRequest(Strategy: strategy, Sources: uris);
        var jobStore = services.GetRequiredService<IIngestionJobStore>();
        var job = await jobStore.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"jobId={job.Id} status={job.Status}");

        var pipeline = services.GetRequiredService<IIngestionPipeline>();
        await jobStore.MarkRunningAsync(job.Id, cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await pipeline.IngestAsync(request, cancellationToken).ConfigureAwait(false);
            await jobStore.MarkSucceededAsync(job.Id, result, cancellationToken).ConfigureAwait(false);

            var completed = await jobStore.GetAsync(job.Id, cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"jobId={job.Id} status={completed?.Status.ToString() ?? "Succeeded"} documents={completed?.DocumentCount ?? 0} chunks={completed?.ChunkCount ?? result.ChunkCount}");
        }
        catch (OperationCanceledException)
        {
            // A cancelled token would also cancel any attempt to mark the job failed, so leave the
            // job as-is and let the caller report cancellation.
            throw;
        }
        catch (Exception exception)
        {
            await jobStore.MarkFailedAsync(job.Id, exception.Message, cancellationToken).ConfigureAwait(false);
            Console.Error.WriteLine($"jobId={job.Id} status=Failed error={exception.Message}");
            throw;
        }
    }

    private static async Task PreviewAsync(string path, IServiceProvider services, CancellationToken cancellationToken)
    {
        var preview = services.GetRequiredService<IChunkPreviewService>();
        var rows = await preview.PreviewAsync(path, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("strategy\tchunks\tavg_size\toverlap\tsample");
        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Strategy}\t{row.ChunkCount}\t{row.AverageSize:F1}\t{row.Overlap}\t{row.Sample.Replace('\n', ' ')}");
        }
    }

    private static async Task QueryAsync(string question, VectorSearchFilter? filter, IServiceProvider services, CancellationToken cancellationToken)
    {
        var pipeline = services.GetRequiredService<IQueryPipeline>();
        var answer = await pipeline.QueryAsync(new QueryRequest(question, Filter: filter), cancellationToken).ConfigureAwait(false);
        Console.WriteLine(answer.Answer);
        foreach (var citation in answer.Citations)
        {
            Console.WriteLine($"- {citation.Source}#{citation.ChunkIndex} score={citation.Score:F3}");
        }
    }

    private static void PrintConfig(IConfiguration configuration)
    {
        Console.WriteLine($"LLM_PROVIDER={configuration["LLM_PROVIDER"] ?? configuration["Llm:Provider"] ?? "deterministic"}");
        Console.WriteLine($"DOC_STORE={configuration["DOC_STORE"] ?? configuration["DocumentStore:Provider"] ?? "memory"}");
        Console.WriteLine($"VECTOR_STORE={configuration["VECTOR_STORE"] ?? configuration["VectorStore:Provider"] ?? "memory"}");
        Console.WriteLine($"CHUNKING_STRATEGY={configuration["CHUNKING_STRATEGY"] ?? configuration["Rag:ChunkingStrategy"] ?? "fixed"}");
    }

    private static async Task PrintJobStatusAsync(string id, IServiceProvider services, CancellationToken cancellationToken)
    {
        var jobs = services.GetRequiredService<IIngestionJobStore>();
        var job = await jobs.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            Console.WriteLine($"jobId={id} status=Unknown");
            Console.WriteLine("CLI jobs are in-memory for the current process.");
            return;
        }

        Console.WriteLine($"jobId={job.Id} status={job.Status} documents={job.DocumentCount} chunks={job.ChunkCount} error={job.Error ?? string.Empty}");
    }
}
