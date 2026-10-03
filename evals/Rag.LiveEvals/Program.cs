using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rag.Core.Workbench;
using Rag.LiveEvals;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: Rag.LiveEvals <run-config.json> <dataset.json> <new-output-directory>. Credentials: RAG_API_KEY and optional RAG_EVAL_JUDGE_KEY.");
    return 2;
}
try
{
    var config = JsonSerializer.Deserialize<RunConfiguration>(await File.ReadAllTextAsync(args[0]), LiveApi.Json) ?? throw new InvalidDataException("Missing configuration.");
    var datasetBytes = await File.ReadAllBytesAsync(args[1]);
    var dataset = JsonSerializer.Deserialize<Dataset>(datasetBytes, LiveApi.Json) ?? throw new InvalidDataException("Missing dataset.");
    if (config.SchemaVersion != 1 || dataset.SchemaVersion != 1 || dataset.Status != "frozen" || config.Split is not ("development" or "held-out") ||
        config.Pricing.Currency != "USD" || !DateOnly.TryParse(config.Pricing.Date, out _) || config.EmbeddingModel == "deterministic" ||
        string.IsNullOrWhiteSpace(config.EmbeddingModel) || string.IsNullOrWhiteSpace(config.ChatModel) || string.IsNullOrWhiteSpace(config.Commit) ||
        config.Pricing.EmbeddingPerMillion <= 0 || config.Pricing.InputPerMillion <= 0 || config.Pricing.OutputPerMillion <= 0)
    { throw new InvalidDataException("Require a frozen dataset, explicit real models, commit, positive dated USD pricing and versioned configuration."); }
    if (Directory.Exists(args[2]))
    {
        throw new IOException("Use a new output directory; existing evidence is never overwritten.");
    }

    var budget = new RunBudget(config.BudgetUsd, config.IngestionEstimatedUsd);
    using var api = new LiveApi(config.ApiUrl, Environment.GetEnvironmentVariable("RAG_API_KEY") ?? throw new InvalidOperationException("Set RAG_API_KEY."));
    using var cancellation = new CancellationTokenSource(TimeSpan.FromHours(1));
    var token = cancellation.Token;
    var caps = await api.GetAsync<JsonElement>("capabilities", token);
    if (caps.GetProperty("llmProvider").GetString() == "deterministic" || caps.GetProperty("embeddingModel").GetString() != config.EmbeddingModel || caps.GetProperty("chatModel").GetString() != config.ChatModel)
    { throw new InvalidDataException("Configured model identity does not match the connected API."); }
    var profiles = await api.GetAsync<List<IndexProfile>>($"corpora/{config.CorpusId}/profiles", token);
    var profile = profiles.Single(item => item.Id == config.ProfileId);
    if (profile.Status != "ready" || profile.Strategy != "recursive")
    {
        throw new InvalidDataException("First baseline requires an indexed recursive profile.");
    }

    async Task<List<T>> Pages<T>(string path)
    {
        var all = new List<T>();
        while (true)
        {
            var page = await api.GetAsync<PageResult<T>>($"{path}?offset={all.Count}&limit=200", token);
            all.AddRange(page.Items);
            if (all.Count >= page.Total)
            {
                return all;
            }

            if (page.Items.Count == 0)
            {
                throw new InvalidDataException("Invalid API pagination.");
            }
        }
    }
    var documents = await Pages<WorkbenchDocument>($"profiles/{profile.Id}/documents");
    foreach (var entry in dataset.DocumentHashes)
    {
        var document = documents.Single(item => item.Filename == entry.Key);
        if (Hash(Encoding.UTF8.GetBytes(document.Content)) != entry.Value)
        {
            throw new InvalidDataException("Corpus hash mismatch.");
        }
    }
    if (documents.Count != dataset.DocumentHashes.Count)
    {
        throw new InvalidDataException("Corpus contains unversioned documents.");
    }

    foreach (var question in dataset.Cases)
    {
        foreach (var anchor in question.Anchors)
        {
            var source = documents.Single(item => item.Filename == anchor.Source).Content;
            if (anchor.Start < 0 || anchor.End > source.Length || anchor.End <= anchor.Start || source[anchor.Start..anchor.End] != anchor.Text)
            {
                throw new InvalidDataException("Gold evidence span does not match corpus.");
            }
        }
    }
    var chunks = await Pages<Chunk>($"profiles/{profile.Id}/chunks");
    var pool = chunks.Select(c => new DetailedCandidate(c.Id, c.DocumentId, c.Filename, c.Index, c.Content, null, null, null, null, 0, 0, false)).ToArray();
    var spans = chunks.ToDictionary(c => c.Id, c => new ChunkSpan(c.Filename, c.StartOffset, c.EndOffset));
    var questions = dataset.Cases.Where(item => item.Split == config.Split).ToArray();
    if (questions.Length == 0 || questions.Select(q => q.Id).Distinct().Count() != questions.Length)
    {
        throw new InvalidDataException("Empty split or duplicate case IDs.");
    }

    var judgeKey = Environment.GetEnvironmentVariable("RAG_EVAL_JUDGE_KEY");
    var judging = config.JudgeEndpoint is not null;
    if (judging && (string.IsNullOrWhiteSpace(judgeKey) || string.IsNullOrWhiteSpace(config.JudgeModel) || config.Pricing.JudgeInputPerMillion <= 0 || config.Pricing.JudgeOutputPerMillion <= 0))
    {
        throw new InvalidDataException("Judge configuration is incomplete.");
    }

    var results = new List<CaseResult>();
    Directory.CreateDirectory(args[2]);
    await File.WriteAllTextAsync(Path.Combine(args[2], "manifest.json"), JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        config.Commit,
        dataset.Version,
        datasetHash = Hash(datasetBytes),
        dataset.DocumentHashes,
        profile,
        config.EmbeddingModel,
        config.ChatModel,
        config.JudgeModel,
        judgeVersion = Judge.Version,
        scorerVersion = Scoring.Version,
        systemPromptHash = caps.GetProperty("systemPromptHash").GetString(),
        environment = new { runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount },
        judgeRubricHash = Hash(Encoding.UTF8.GetBytes(Judge.Rubric)),
        config.Pricing,
        config.Split,
        config.BudgetUsd,
        config.IngestionEstimatedUsd,
        startedAt = DateTimeOffset.UtcNow,
        concurrency = 1,
        warmup = "none; cold requests included",
        timing = "Stopwatch including HTTP and stage events",
        caps,
        settings = new { topK = 5, modes = Scoring.Modes, reranker = false, config.MinRelevance, config.MaxContextTokens },
        limitations = (string[])["Indexing estimate is supplied by operator and reserved before query execution; this runner does not perform indexing.", "Budget is an estimate, not a billing guarantee. Provider billing caps remain required.", "Semantic judge scores require human review; generated labels must be reviewed before freezing.", "Query pricing is nullable when provider usage is missing; reservation includes worst-case retries and lexical re-embedding.", "Stable anchors are automatically judged; partial overlap remains conservative and needs human judgments for full NDCG coverage."]
    }, LiveApi.Json));
    foreach (var mode in Scoring.Modes)
    {
        foreach (var question in questions)
        {
            // Byte-count upper estimate plus context/prompt overhead; provider-specific tokenizers may differ.
            var inputCeiling = 8192 + config.MaxContextTokens * 16 + Encoding.UTF8.GetByteCount(question.Question);
            var embeddingCeiling = Encoding.UTF8.GetByteCount(question.Question) + (mode == "hybrid" ? chunks.Sum(c => Encoding.UTF8.GetByteCount(c.Content)) : 0);
            var attempts = caps.GetProperty("retryCount").GetInt32() + 1;
            var reserve = attempts * (inputCeiling * config.Pricing.InputPerMillion + caps.GetProperty("maxOutputTokens").GetInt32() * config.Pricing.OutputPerMillion + embeddingCeiling * config.Pricing.EmbeddingPerMillion) / 1_000_000;
            if (!budget.TryReserve(reserve)) { results.Add(new(question.Id, mode, "budget-exhausted", 0, null, null, null, "Not run", 0, null, null, "Insufficient estimated budget.")); continue; }
            var clock = Stopwatch.StartNew();
            CaseResult result;
            try
            {
                var before = await Pages<WorkbenchDocument>($"profiles/{profile.Id}/documents");
                if (JsonSerializer.Serialize(before.OrderBy(d => d.Id)) != JsonSerializer.Serialize(documents.OrderBy(d => d.Id)))
                {
                    throw new InvalidDataException("Corpus changed during evaluation.");
                }

                clock.Restart();
                var run = await api.QueryAsync(new DetailedQueryRequest(question.Question, config.CorpusId, config.ProfileId, 5, mode, false, config.MinRelevance, false, config.MaxContextTokens), token);
                var duration = clock.Elapsed.TotalMilliseconds;
                if (run.EmbeddingModel != config.EmbeddingModel || run.ChatModel != config.ChatModel || run.Provider == "deterministic" || run.SystemPromptHash != caps.GetProperty("systemPromptHash").GetString())
                { throw new InvalidDataException("Provider identity changed during the run."); }
                var after = await Pages<WorkbenchDocument>($"profiles/{profile.Id}/documents");
                if (JsonSerializer.Serialize(after.OrderBy(d => d.Id)) != JsonSerializer.Serialize(documents.OrderBy(d => d.Id)))
                {
                    throw new InvalidDataException("Corpus changed during evaluation.");
                }

                decimal? queryCost = run.EmbeddingTokens is not null && run.PromptTokens is not null && run.TokenUsageKind.StartsWith("provider", StringComparison.Ordinal)
                ? (run.EmbeddingTokens.Value * config.Pricing.EmbeddingPerMillion + run.PromptTokens.Value * config.Pricing.InputPerMillion + run.OutputTokens * config.Pricing.OutputPerMillion) / 1_000_000 : null;
                SemanticJudgment? judgment = null;
                decimal? judgeCost = null;
                string? judgeFailure = judging ? null : "No semantic judge configured.";
                if (judging)
                {
                    var judgeReserve = (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new { question, run })) * config.Pricing.JudgeInputPerMillion + 4096 * config.Pricing.JudgeInputPerMillion + 2048 * config.Pricing.JudgeOutputPerMillion) / 1_000_000;
                    if (!budget.TryReserve(judgeReserve))
                    {
                        judgeFailure = "Judge budget exhausted.";
                    }
                    else
                    {
                        reserve += judgeReserve;
                        try { (judgment, judgeCost) = await Judge.EvaluateAsync(config, question, run, judgeKey!, token); }
                        catch (Exception error) when (error is not OutOfMemoryException) { judgeFailure = error.GetType().Name; }
                    }
                }
                result = new(question.Id, mode, "complete", duration, run, Scoring.Depths.ToDictionary(k => k, k => Scoring.Retrieval(question, run.Candidates, pool, k, spans)), judgment, judgeFailure, reserve, queryCost, judgeCost, null);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            { result = new(question.Id, mode, "failed", clock.Elapsed.TotalMilliseconds, null, null, null, "Query failed", reserve, null, null, error.GetType().Name); }
            results.Add(result);
            await File.AppendAllTextAsync(Path.Combine(args[2], "cases.jsonl"), JsonSerializer.Serialize(result, JsonSerializerOptions.Web) + "\n");
        }
    }

    await Reports.WriteAsync(args[2], results, questions, budget.Reserved);
    Console.WriteLine($"Wrote {results.Count} outcomes. Estimated reservations: ${budget.Reserved:F4}. No quality threshold inferred from this run.");
    return results.Any(item => item.Status != "complete") ? 1 : 0;
}
catch (Exception error) when (error is not OutOfMemoryException)
{
    Console.Error.WriteLine($"Evaluation stopped ({error.GetType().Name}). Check configuration, dataset, and private service diagnostics; remote details are suppressed.");
    return 1;
}

static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
internal sealed record Chunk(string Id, string DocumentId, string Filename, int Index, string Content, int StartOffset, int EndOffset);
