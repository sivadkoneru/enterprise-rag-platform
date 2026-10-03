using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Common;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Workbench;

public sealed class DetailedQueryPipeline(WorkbenchCatalog catalog, IEmbeddingClient embeddings, WorkbenchVectorStores vectorStores, IChatClient chat, IRerankerClient reranker, IOptions<LlmOptions> llm, IOptions<VectorStoreOptions> vectorOptions)
{
    public async Task<DetailedRun> QueryAsync(DetailedQueryRequest request, Func<DetailedStage, CancellationToken, Task>? onStage = null, CancellationToken token = default)
    {
        using var queryActivity = RagTelemetry.Activities.StartActivity("query");
        Validate(request);
        var profile = await catalog.GetProfileAsync(request.ProfileId, token).ConfigureAwait(false);
        var vectors = vectorStores.ForProfile(profile);
        if (profile.CorpusId != request.CorpusId)
        {
            throw new ArgumentException("The indexing profile does not belong to the selected corpus.");
        }

        if (profile.Status != "ready")
        {
            throw new ArgumentException("Select a fully indexed profile before querying.");
        }

        if (profile.EmbeddingDimensions != llm.Value.EmbeddingDimensions || profile.EmbeddingModel != WorkbenchModelIdentity.EmbeddingModel(llm.Value))
        {
            throw new ArgumentException("The profile embedding configuration differs from the active endpoint; reindex with the active configuration.");
        }

        var totalClock = Stopwatch.StartNew();
        var trace = new List<DetailedStage>();
        var allChunks = await catalog.GetChunksAsync(profile.Id, token: token).ConfigureAwait(false);
        IReadOnlyList<float> queryVector = [];
        IReadOnlyList<VectorSearchResult> matches = [];
        var candidates = new List<DetailedCandidate>();
        var context = new List<DetailedCandidate>();
        var answer = "";
        var citations = new List<DetailedCitation>();
        var invalid = new List<string>();
        var estimatedContextTokens = 0;
        var extraEmbeddingCalls = 0;
        ChatCompletionResult? completion = null;
        int? embeddingTokens = 0;
        var embeddingCalls = 0;
        async Task<IReadOnlyList<float>> EmbedAsync(string text)
        {
            embeddingCalls++;
            if (embeddings is IEmbeddingUsageClient detailed)
            {
                var result = await detailed.EmbedDetailedAsync(text, token).ConfigureAwait(false);
                embeddingTokens = embeddingTokens is not null && result.InputTokens is not null ? embeddingTokens + result.InputTokens : null;
                return result.Vector;
            }
            embeddingTokens = null;
            return await embeddings.EmbedAsync(text, token).ConfigureAwait(false);
        }
        async Task StageAsync(string id, string name, Func<Task> action, string detail, IReadOnlyDictionary<string, object?>? diagnostics = null, bool skip = false)
        {
            var stage = new DetailedStage(id, name, skip ? "skipped" : "running", 0, detail, diagnostics ?? new Dictionary<string, object?>());
            trace.Add(stage);
            if (onStage is not null)
            {
                await onStage(stage, token).ConfigureAwait(false);
            }

            if (skip)
            {
                return;
            }

            using var stageActivity = RagTelemetry.Activities.StartActivity($"query.{id}");
            var clock = Stopwatch.StartNew();
            try
            {
                await action().ConfigureAwait(false);
                stage = stage with { Status = "complete", DurationMs = clock.ElapsedMilliseconds };
                RagTelemetry.StageLatency.Record(clock.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("stage", id));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                stage = stage with { Status = exception is OperationCanceledException ? "canceled" : "failed", DurationMs = clock.ElapsedMilliseconds, Detail = exception is OperationCanceledException ? "Request canceled" : "Stage failed; inspect server diagnostics" };
                trace[^1] = stage;
                if (onStage is not null && !token.IsCancellationRequested)
                {
                    await onStage(stage, token).ConfigureAwait(false);
                }

                throw;
            }
            trace[^1] = stage;
            if (onStage is not null)
            {
                await onStage(stage, token).ConfigureAwait(false);
            }
        }
        await StageAsync("query", "Query validation", () => Task.CompletedTask, "Validated corpus, profile, and retrieval controls", new Dictionary<string, object?> { ["profileId"] = profile.Id }).ConfigureAwait(false);
        await StageAsync("enhancement", "Query normalization", () => Task.CompletedTask, "No query enhancement provider configured", skip: true).ConfigureAwait(false);
        await StageAsync("embedding", "Embedding", async () =>
        {
            queryVector = await EmbedAsync(request.Question.Trim()).ConfigureAwait(false);
            EmbeddingValidation.Validate(queryVector, profile.EmbeddingDimensions);
        }, "Embedding query with the configured provider", new Dictionary<string, object?> { ["model"] = profile.EmbeddingModel, ["dimensions"] = profile.EmbeddingDimensions, ["provider"] = llm.Value.Provider }).ConfigureAwait(false);
        await StageAsync("search", request.Mode == "hybrid" ? "Vector + BM25 hybrid search" : "Vector search", async () =>
        {
            if (profile.DocumentIds.Count == 0)
            {
                return;
            }

            var pool = Math.Min(400, Math.Max(request.TopK, request.TopK * 4));
            matches = await vectors.SearchAsync(queryVector, pool, new VectorSearchFilter(DocumentIds: profile.DocumentIds), token).ConfigureAwait(false);
            var byId = allChunks.ToDictionary(chunk => chunk.Id, StringComparer.Ordinal);
            var validMatches = matches.Where(match => byId.ContainsKey(match.ChunkId)).DistinctBy(match => match.ChunkId).ToArray();
            var vectorRanks = validMatches.Select((match, i) => (match.ChunkId, Rank: i + 1)).ToDictionary(pair => pair.ChunkId, pair => pair.Rank);
            var vectorScores = validMatches.ToDictionary(match => match.ChunkId, match => vectorOptions.Value.Provider.Equals("elasticsearch", StringComparison.OrdinalIgnoreCase) ? Math.Clamp(match.Score, 0, 1) : Math.Clamp((match.Score + 1) / 2, 0, 1));
            KeyValuePair<string, double>[] lexical = [];
            IReadOnlyDictionary<string, double> lexicalScores = new Dictionary<string, double>();
            if (request.Mode == "hybrid")
            {
                if (vectors is ILexicalSearchStore lexicalStore)
                {
                    lexical = (await lexicalStore.SearchLexicalAsync(request.Question, pool, new VectorSearchFilter(DocumentIds: profile.DocumentIds), token).ConfigureAwait(false)).Where(match => byId.ContainsKey(match.ChunkId)).DistinctBy(match => match.ChunkId).Select(match => new KeyValuePair<string, double>(match.ChunkId, match.Score)).ToArray();
                    lexicalScores = lexical.ToDictionary(pair => pair.Key, pair => pair.Value);
                }
                else
                {
                    lexicalScores = Bm25(request.Question, allChunks);
                    lexical = lexicalScores.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Where(pair => pair.Value > 0).Take(pool).ToArray();
                }
            }
            var lexicalRanks = lexical.Select((pair, i) => (pair.Key, Rank: i + 1)).ToDictionary(pair => pair.Key, pair => pair.Rank);
            foreach (var id in validMatches.Select(match => match.ChunkId).Union(lexical.Select(pair => pair.Key)))
            {
                var chunk = byId[id];
                if (!vectorScores.TryGetValue(id, out var vectorScore))
                {
                    var embedding = await EmbedAsync(chunk.Text).ConfigureAwait(false);
                    EmbeddingValidation.Validate(embedding, queryVector.Count);

                    extraEmbeddingCalls++;
                    vectorScore = Math.Clamp((VectorMath.CosineSimilarity(queryVector, embedding) + 1) / 2, 0, 1);
                }
                var fusion = request.Mode == "hybrid" ? (vectorRanks.TryGetValue(id, out var vr) ? 1d / (60 + vr) : 0) + (lexicalRanks.TryGetValue(id, out var lr) ? 1d / (60 + lr) : 0) : (double?)null;
                candidates.Add(new DetailedCandidate(id, chunk.DocumentId, chunk.Metadata.FileName, chunk.Index, chunk.Text, vectorScore, lexicalScores.TryGetValue(id, out var lexicalScore) ? lexicalScore : null, fusion, null, 0, 0, false));
            }
            candidates = candidates.OrderByDescending(candidate => request.Mode == "hybrid" ? candidate.FusionScore : candidate.VectorScore).ThenBy(candidate => candidate.Id, StringComparer.Ordinal).Take(request.TopK).Select((candidate, index) => candidate with { RankBefore = index + 1, RankAfter = index + 1 }).ToList();
        }, "Scores are normalized cosine [0,1]; hybrid uses BM25 + reciprocal rank fusion k=60", new Dictionary<string, object?> { ["mode"] = request.Mode, ["topK"] = request.TopK, ["lexicalAlgorithm"] = "BM25 k1=1.2 b=0.75 on the isolated profile", ["fusionK"] = 60 }).ConfigureAwait(false);
        await StageAsync("reranking", "HTTP reranking", async () =>
        {
            if (candidates.Count == 0)
            {
                return;
            }

            var scores = await reranker.RerankAsync(request.Question, candidates.Select(candidate => candidate.Content).ToArray(), token).ConfigureAwait(false);
            candidates = candidates.Select((candidate, index) => candidate with { RerankerScore = scores[index] }).OrderByDescending(candidate => candidate.RerankerScore).ThenBy(candidate => candidate.RankBefore).Select((candidate, index) => candidate with { RankAfter = index + 1 }).ToList();
        }, request.Reranker ? "Real HTTP provider scores; the candidate pool remains fixed" : "Disabled", skip: !request.Reranker).ConfigureAwait(false);
        await StageAsync("context", "Context assembly", () =>
        {
            var remaining = request.MaxContextTokens;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var estimated = EstimateTokens(candidate.Content);
                if (candidate.VectorScore < request.MinRelevance)
                {
                    candidate = candidate with { ExclusionReason = "Below normalized vector similarity threshold" };
                }
                else if (estimated > remaining)
                {
                    candidate = candidate with { ExclusionReason = "Exceeds remaining context budget" };
                }
                else { candidate = candidate with { InContext = true }; context.Add(candidate); remaining -= estimated; }
                candidates[index] = candidate;
            }
            if (request.Neighbors)
            {
                foreach (var parent in context.ToArray())
                {
                    foreach (var neighbor in allChunks.Where(chunk => chunk.DocumentId == parent.DocumentId && Math.Abs(chunk.Index - parent.Index) == 1).OrderBy(chunk => chunk.Index))
                    {
                        if (context.Any(chunk => chunk.Id == neighbor.Id) || candidates.Any(chunk => chunk.Id == neighbor.Id))
                        {
                            continue;
                        }

                        var estimated = EstimateTokens(neighbor.Text);
                        if (estimated > remaining)
                        {
                            continue;
                        }

                        context.Add(new DetailedCandidate(neighbor.Id, neighbor.DocumentId, neighbor.Metadata.FileName, neighbor.Index, neighbor.Text, null, null, null, null, 0, 0, true, IsNeighbor: true));
                        remaining -= estimated;
                    }
                }
            }
            estimatedContextTokens = context.Sum(candidate => EstimateTokens(candidate.Content));
            return Task.CompletedTask;
        }, "Vector threshold applies before context admission; whole chunks and same-document neighbors respect the budget", new Dictionary<string, object?> { ["budget"] = request.MaxContextTokens, ["tokenEstimate"] = "ceil(chars/4)", ["extraHybridEmbeddingCalls"] = extraEmbeddingCalls }).ConfigureAwait(false);
        var abstained = context.Count == 0;
        await StageAsync("generation", "Answer generation", async () =>
        {
            if (abstained) { answer = "I don't know based on the supplied context."; return; }
            var prompt = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"Question: {request.Question}").AppendLine("Use bracketed chunk IDs from the context as citations. Do not invent references.").AppendLine("Context:");
            foreach (var chunk in context)
            {
                prompt.AppendLine(CultureInfo.InvariantCulture, $"[{chunk.Id}] source={chunk.Filename}").AppendLine(chunk.Content);
            }

            if (llm.Value.Provider.Equals("deterministic", StringComparison.OrdinalIgnoreCase))
            {
                var queryWords = Words(request.Question).Where(word => word.Length > 2 && !QuestionStopWords.Contains(word)).Select(NormalizePlural).ToHashSet(StringComparer.Ordinal);
                var passages = context.SelectMany((chunk, rank) => Regex.Split(chunk.Content, @"(?<=[.!?])\s+|\r?\n+", RegexOptions.CultureInvariant).Select(sentence => new { Chunk = chunk, Sentence = sentence.Trim(), Rank = rank }))
                    .Where(passage => Words(passage.Sentence).Length >= 5)
                    .Select(passage => new { passage.Chunk, passage.Sentence, passage.Rank, Matches = Words(passage.Sentence).Select(NormalizePlural).Distinct(StringComparer.Ordinal).Count(queryWords.Contains) })
                    .OrderByDescending(passage => passage.Matches).ThenBy(passage => passage.Rank).ToArray();
                if (passages.Length > 0) { answer = $"{passages[0].Sentence} [{passages[0].Chunk.Id}]"; }
                else { answer = $"{context[0].Content.Trim()} [{context[0].Id}]"; }
            }
            else
            {
                var messages = new[] { new ChatMessage("system", llm.Value.SystemPrompt), new ChatMessage("user", prompt.ToString()) };
                if (chat is IChatUsageClient detailed)
                {
                    completion = await detailed.CompleteDetailedAsync(messages, token).ConfigureAwait(false);
                    answer = completion.Text;
                }
                else { answer = await chat.CompleteAsync(messages, token).ConfigureAwait(false); }
            }
            abstained = IsAbstention(answer);
        }, "Configured chat provider; abstain locally only when no context is admitted", new Dictionary<string, object?> { ["provider"] = llm.Value.Provider, ["model"] = WorkbenchModelIdentity.ChatModel(llm.Value) }).ConfigureAwait(false);
        await StageAsync("validation", "Citation validation", () =>
        {
            var contextById = context.ToDictionary(chunk => chunk.Id, StringComparer.Ordinal);
            foreach (Match citation in Regex.Matches(answer, @"\[([^\[\]\r\n]+)\]", RegexOptions.CultureInvariant))
            {
                var id = citation.Groups[1].Value.Trim();
                if (int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 && number <= context.Count)
                {
                    var original = id;
                    id = context[number - 1].Id;
                    answer = answer.Replace($"[{original}]", $"[{id}]", StringComparison.Ordinal);
                }

                if (!contextById.TryGetValue(id, out var chunk)) { if (!invalid.Contains(id)) { invalid.Add(id); citations.Add(new DetailedCitation(citations.Count + 1, id, "", false)); } continue; }
                if (!citations.Any(existing => existing.ChunkId == id))
                {
                    citations.Add(new DetailedCitation(citations.Count + 1, id, chunk.DocumentId, true));
                }
            }
            return Task.CompletedTask;
        }, "Only citations emitted by the model and resolving to admitted context are valid", new Dictionary<string, object?> { ["validation"] = "ID resolution; does not prove semantic entailment" }).ConfigureAwait(false);
        return new DetailedRun(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, request.Question, profile.CorpusId, profile.Id, answer, citations, candidates, context, trace, totalClock.ElapsedMilliseconds, estimatedContextTokens, completion?.CompletionTokens ?? EstimateTokens(answer), completion?.CompletionTokens is null ? "estimated-chars/4" : "provider-output/context-estimated-chars/4", abstained, invalid) { Provider = llm.Value.Provider, EmbeddingModel = profile.EmbeddingModel, ChatModel = WorkbenchModelIdentity.ChatModel(llm.Value), SystemPromptHash = WorkbenchModelIdentity.SystemPromptHash(llm.Value), TraceId = Activity.Current?.TraceId.ToString(), EmbeddingTokens = embeddingTokens, EmbeddingCalls = embeddingCalls, PromptTokens = completion?.PromptTokens, TotalTokens = completion?.TotalTokens };
    }
    public static void Validate(DetailedQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 8000 || string.IsNullOrWhiteSpace(request.CorpusId) || string.IsNullOrWhiteSpace(request.ProfileId))
        {
            throw new ArgumentException("Provide a question up to 8,000 characters, corpus, and indexing profile.");
        }

        if (request.TopK is < 1 or > 100 || request.Mode is not ("vector" or "hybrid") || !double.IsFinite(request.MinRelevance) || request.MinRelevance is < 0 or > 1 || request.MaxContextTokens is < 128 or > 65536)
        {
            throw new ArgumentException("Invalid retrieval controls.");
        }
    }
    private static readonly HashSet<string> QuestionStopWords = new(StringComparer.Ordinal) { "what", "which", "where", "when", "does", "the", "and", "are", "how", "for", "with", "that", "from", "can", "have", "has", "apply" };
    private static string NormalizePlural(string word) => word.Length > 3 && word.EndsWith('s') ? word[..^1] : word;
    public static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / 4d);
    public static bool IsAbstention(string answer) => Regex.IsMatch(answer, @"^\s*(I (don't|do not) know|I (cannot|can't) (answer|determine)|insufficient (evidence|context)|not enough (evidence|context))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static IReadOnlyDictionary<string, double> Bm25(string question, IReadOnlyList<TextChunk> chunks)
    {
        var query = Words(question).Distinct(StringComparer.Ordinal).ToArray();
        var documents = chunks.Select(chunk => (chunk.Id, Words: Words(chunk.Text))).ToArray();
        var average = Math.Max(1, documents.Length == 0 ? 1 : documents.Average(document => document.Words.Length));
        var frequencies = query.ToDictionary(word => word, word => documents.Count(document => document.Words.Contains(word, StringComparer.Ordinal)), StringComparer.Ordinal);
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            var score = 0d;
            foreach (var word in query)
            {
                var frequency = document.Words.Count(token => token == word);
                var idf = Math.Log(1 + (documents.Length - frequencies[word] + 0.5) / (frequencies[word] + 0.5));
                score += idf * frequency * 2.2 / (frequency + 1.2 * (0.25 + 0.75 * document.Words.Length / average));
            }
            scores[document.Id] = score;
        }
        return scores;
    }
    public static string[] Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant).Select(match => match.Value).ToArray();
}
