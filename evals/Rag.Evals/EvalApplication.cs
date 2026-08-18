using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Rag.Core.Parsing;
using Rag.Evals.Corpus;
using Rag.Evals.Dataset;
using Rag.Evals.Composition;
using Rag.Evals.Reporting;
using Rag.Evals.Scoring;

namespace Rag.Evals;

/// <summary>
/// Command surface for the evaluation harness. Mirrors <c>Rag.Cli</c>'s conventions: handlers set
/// <see cref="InvocationContext.ExitCode"/>, failures report 2, and output is flat key=value or TSV
/// so it stays greppable.
/// </summary>
internal static class EvalApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        return await BuildRootCommand().InvokeAsync(args).ConfigureAwait(false);
    }

    private static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("Evaluation and chunking benchmark harness for the Enterprise RAG Platform.");

        var outOption = new Option<string?>(
            "--out",
            description: "Destination path. Defaults to samples/handbook.pdf.");
        var generate = new Command("generate-handbook", "Render the synthetic handbook corpus to a PDF.")
        {
            outOption
        };
        generate.SetHandler(
            context => RunSafely(context, () =>
            {
                var target = context.ParseResult.GetValueForOption(outOption) ?? RepoPaths.HandbookPdf;
                HandbookPdfWriter.Write(target);
                var info = new FileInfo(target);
                Console.WriteLine(Format("path", target));
                Console.WriteLine(Format("bytes", info.Length));
                return Task.CompletedTask;
            }));
        root.AddCommand(generate);

        var rulerOption = new Option<bool>(
            "--ruler",
            description: "Prefix each line with its character offset in the parsed text.");
        var pathArgument = new Argument<string?>("path", () => null, "Document to parse. Defaults to samples/handbook.pdf.");
        var dump = new Command("dump-text", "Print the parsed, normalized text a chunking strategy actually sees.")
        {
            pathArgument,
            rulerOption
        };
        dump.SetHandler(context => RunSafely(context, async () =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument) ?? RepoPaths.HandbookPdf;
            var text = await ParseAsync(path).ConfigureAwait(false);
            Console.WriteLine(Format("path", path));
            Console.WriteLine(Format("chars", text.Length));
            Console.WriteLine(Format("paragraphs", text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Length));
            Console.WriteLine();
            Console.Write(context.ParseResult.GetValueForOption(rulerOption) ? WithRuler(text) : text);
            Console.WriteLine();
        }));
        root.AddCommand(dump);

        var datasetOption = new Option<string?>(
            "--dataset",
            description: "Golden dataset path. Defaults to the copy beside the binary.");
        var validate = new Command("validate", "Resolve every gold anchor against the corpus and report lexical overlap.")
        {
            datasetOption
        };
        validate.SetHandler(context => RunSafely(context, async () =>
        {
            var dataset = GoldenDatasetLoader.Load(context.ParseResult.GetValueForOption(datasetOption));
            var corpus = await ParseAsync(RepoPaths.HandbookPdf).ConfigureAwait(false);
            var resolver = new AnchorResolver(corpus);
            var resolved = GoldenDatasetLoader.Resolve(dataset, resolver);

            Console.WriteLine("id\ttype\tdeclared\tmeasured\toverlap\tanchors");
            var mismatches = 0;
            foreach (var item in resolved)
            {
                var overlap = GoldenDatasetLoader.LexicalOverlap(item);
                var measured = item.Question.IsAnswerable
                    ? GoldenDatasetLoader.BandFor(overlap).ToString().ToLowerInvariant()
                    : item.Question.Difficulty.ToString().ToLowerInvariant();
                var declared = item.Question.Difficulty.ToString().ToLowerInvariant();
                if (!string.Equals(declared, measured, StringComparison.Ordinal))
                {
                    mismatches++;
                }

                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.Question.Id}\t{item.Question.Type.ToString().ToLowerInvariant()}\t{declared}\t{measured}\t{overlap:F3}\t{item.Anchors.Count}"));
            }

            Console.WriteLine();
            Console.WriteLine(Format("questions", resolved.Count));
            Console.WriteLine(Format("anchorsResolved", resolved.Sum(item => item.Anchors.Count)));
            Console.WriteLine(Format("difficultyMismatches", mismatches));
        }));
        root.AddCommand(validate);

        var run = new Command("run", "Ingest the corpus under every chunking strategy and score the golden dataset.")
        {
            datasetOption
        };
        run.SetHandler(context => RunSafely(context, async () =>
        {
            var result = await ExecuteAsync(context.ParseResult.GetValueForOption(datasetOption)).ConfigureAwait(false);
            ResultsWriter.WriteJson(Path.Combine(RepoPaths.Results, "latest.json"), result);

            Console.WriteLine(MarkdownTable.Profile(result));
            Console.Write(MarkdownTable.BenchmarkTable(result));
            Console.WriteLine();
            Console.Write(MarkdownTable.DifficultyTable(result));
            Console.WriteLine();
            Console.Write(MarkdownTable.IntegrityTable(result));
        }));
        root.AddCommand(run);

        var writeOption = new Option<bool>(
            "--write",
            description: "Write evals/results and inject the tables into README.md.");
        var report = new Command("report", "Run the evaluation and render the published tables.")
        {
            datasetOption,
            writeOption
        };
        report.SetHandler(context => RunSafely(context, async () =>
        {
            var result = await ExecuteAsync(context.ParseResult.GetValueForOption(datasetOption)).ConfigureAwait(false);
            var benchmark = BuildBenchmarkDocument(result);

            if (!context.ParseResult.GetValueForOption(writeOption))
            {
                Console.Write(benchmark);
                return;
            }

            ResultsWriter.WriteJson(Path.Combine(RepoPaths.Results, "latest.json"), result);
            ResultsWriter.Write(Path.Combine(RepoPaths.Results, "benchmark.md"), benchmark);

            var readme = File.ReadAllText(RepoPaths.Readme);
            readme = MarkerSectionWriter.Replace(readme, "EVAL-TABLE", MarkdownTable.BenchmarkTable(result));
            readme = MarkerSectionWriter.Replace(readme, "EVAL-DIFFICULTY", MarkdownTable.DifficultyTable(result));
            readme = MarkerSectionWriter.Replace(readme, "EVAL-INTEGRITY", MarkdownTable.IntegrityTable(result));
            readme = MarkerSectionWriter.Replace(readme, "EVAL-PROFILE", MarkdownTable.Profile(result));
            ResultsWriter.Write(RepoPaths.Readme, readme);

            Console.WriteLine(Format("results", Path.Combine(RepoPaths.Results, "latest.json")));
            Console.WriteLine(Format("benchmark", Path.Combine(RepoPaths.Results, "benchmark.md")));
            Console.WriteLine(Format("readme", RepoPaths.Readme));
        }));
        root.AddCommand(report);

        return root;
    }



    /// <summary>The standalone benchmark document committed under evals/results.</summary>
    private static string BuildBenchmarkDocument(EvalRun result)
    {
        var builder = new StringBuilder();
        builder.Append("# Chunking Strategy Benchmark\n\n");
        builder.Append("Generated by `dotnet run --project evals/Rag.Evals -- report --write`. Do not edit by hand.\n\n");
        builder.Append(MarkdownTable.Profile(result));
        builder.Append("\n## Retrieval and citations\n\n");
        builder.Append(MarkdownTable.BenchmarkTable(result));
        builder.Append("\n## Retrieval by question difficulty\n\n");
        builder.Append(MarkdownTable.DifficultyTable(result));
        builder.Append("\n## Grounding and abstention\n\n");
        builder.Append(MarkdownTable.IntegrityTable(result));
        return builder.ToString();
    }

    /// <summary>Parses the corpus, resolves the dataset against it, and runs every strategy.</summary>
    internal static async Task<EvalRun> ExecuteAsync(string? datasetPath)
    {
        var dataset = GoldenDatasetLoader.Load(datasetPath);
        var corpus = await ParseAsync(RepoPaths.HandbookPdf).ConfigureAwait(false);
        var resolved = GoldenDatasetLoader.Resolve(dataset, new AnchorResolver(corpus));

        var distractors = Directory.Exists(RepoPaths.Distractors)
            ? Directory.GetFiles(RepoPaths.Distractors, "*.md").OrderBy(path => path, StringComparer.Ordinal).ToArray()
            : [];
        var corpusPaths = new List<string> { RepoPaths.HandbookPdf };
        corpusPaths.AddRange(distractors);

        var profile = RunProfile.Default([.. corpusPaths.Select(Path.GetFileName).Where(name => name is not null).Cast<string>()]);
        return await EvalRunner.RunAsync(profile, corpusPaths, resolved).ConfigureAwait(false);
    }

    internal static async Task<string> ParseAsync(string path)
    {
        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = await new PdfDocumentParser(NullLogger<PdfDocumentParser>.Instance)
                .ParseAsync(path)
                .ConfigureAwait(false);
            return parsed.Text;
        }

        if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return (await new MarkdownDocumentParser().ParseAsync(path).ConfigureAwait(false)).Text;
        }

        return (await new TxtDocumentParser().ParseAsync(path).ConfigureAwait(false)).Text;
    }

    /// <summary>Annotates each line with the character offset it starts at, for authoring anchors.</summary>
    private static string WithRuler(string text)
    {
        var builder = new StringBuilder();
        var offset = 0;
        foreach (var line in text.Split('\n'))
        {
            builder.Append(offset.ToString("D6", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(line)
                .Append('\n');
            offset += line.Length + 1;
        }

        return builder.ToString();
    }

    private static string Format(string key, object value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{key}={value}");
    }

    /// <summary>
    /// Mirrors Rag.Cli: every failure reports exit code 2 with the message on stderr, so a broken
    /// eval fails a CI step rather than printing a stack trace and succeeding.
    /// </summary>
    private static async Task RunSafely(InvocationContext context, Func<Task> action)
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
}
