using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Llm;
using Rag.Core.Models;
using Rag.Core.Stores;
using Rag.Core.Vector;
using Rag.Core.Workbench;
using Xunit;

namespace Rag.Core.Tests;

public sealed class WorkbenchTests
{
    [Fact]
    public async Task ProfileSettingsAreImmutableAndEmbeddingConfigurationMustMatch()
    {
        var fixture = await Fixture.CreateAsync();
        var profile = await fixture.Catalog.CreateProfileAsync(fixture.Corpus.Id, new CreateProfileRequest("Another", "recursive", 300, 30, "test", 2));
        profile.SemanticDistanceThreshold.Should().Be(0.31);
        profile.Status.Should().Be("empty");
        var invalid = async () => await fixture.Catalog.CreateProfileAsync(fixture.Corpus.Id, new CreateProfileRequest("Invalid", "fixed", EmbeddingDimensions: 3));
        await invalid.Should().ThrowAsync<ArgumentException>();
        (await fixture.Catalog.ListProfilesAsync(fixture.Corpus.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task VectorThresholdUsesNormalizedCosineBeforeContextAdmission()
    {
        var fixture = await Fixture.CreateAsync();
        var run = await fixture.Pipeline.QueryAsync(fixture.Request() with { MinRelevance = 0.9 });
        run.Candidates.Should().HaveCount(3);
        run.Candidates[0].VectorScore.Should().Be(1);
        run.Context.Should().ContainSingle().Which.Id.Should().Be("c1");
        run.Candidates.Where(candidate => !candidate.InContext).Should().OnlyContain(candidate => candidate.ExclusionReason == "Below normalized vector similarity threshold");
    }

    [Fact]
    public async Task QueryCannotRetrieveAnotherProfilesChunkEvenWhenVectorRanksItHigher()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Vectors.UpsertAsync([new VectorRecord("foreign", "other-document", [1, 0], new Dictionary<string, string>())]);
        var run = await fixture.Pipeline.QueryAsync(fixture.Request());
        run.Candidates.Should().NotContain(candidate => candidate.Id == "foreign");
        run.Context.Should().OnlyContain(candidate => fixture.Profile.DocumentIds.Contains(candidate.DocumentId));
    }

    [Fact]
    public async Task VectorOnlyRetrievalLeavesUnavailableLexicalScoresNull()
    {
        var fixture = await Fixture.CreateAsync();
        var run = await fixture.Pipeline.QueryAsync(fixture.Request() with { Mode = "vector" });
        run.Candidates.Should().NotBeEmpty().And.OnlyContain(candidate => candidate.LexicalScore == null);
        run.Context.Should().OnlyContain(candidate => candidate.LexicalScore == null);
    }

    [Fact]
    public async Task HybridScoresUseActualLexicalMatchesAndReciprocalRankFusion()
    {
        var fixture = await Fixture.CreateAsync();
        var run = await fixture.Pipeline.QueryAsync(fixture.Request() with { Question = "API credential", Mode = "hybrid" });
        run.Candidates.Single(candidate => candidate.Id == "c2").LexicalScore.Should().BeGreaterThan(0);
        run.Candidates.Single(candidate => candidate.Id == "c1").LexicalScore.Should().Be(0);
        run.Candidates.Should().OnlyContain(candidate => candidate.FusionScore > 0);
        run.Trace.Single(stage => stage.Id == "search").Diagnostics["fusionK"].Should().Be(60);
        run.Trace.Single(stage => stage.Id == "enhancement").Status.Should().Be("skipped");
    }

    [Fact]
    public async Task RerankingChangesOnlyTheSelectedCandidatePool()
    {
        var fixture = await Fixture.CreateAsync();
        var raw = await fixture.Pipeline.QueryAsync(fixture.Request() with { TopK = 2 });
        var reranked = await fixture.Pipeline.QueryAsync(fixture.Request() with { TopK = 2, Reranker = true });
        reranked.Candidates.Select(candidate => candidate.Id).Should().BeEquivalentTo(raw.Candidates.Select(candidate => candidate.Id));
        reranked.Candidates[0].Id.Should().NotBe(raw.Candidates[0].Id);
        reranked.Candidates.Should().Contain(candidate => candidate.RankBefore != candidate.RankAfter);
    }

    [Fact]
    public async Task NeighborContextHasUnknownSimilarityAndStaysWithinTheBudget()
    {
        var fixture = await Fixture.CreateAsync();
        var run = await fixture.Pipeline.QueryAsync(fixture.Request() with { TopK = 1, Neighbors = true, MaxContextTokens = 128 });
        run.Context.Should().HaveCount(2);
        run.Context.Single(candidate => candidate.IsNeighbor).VectorScore.Should().BeNull();
        run.ContextTokens.Should().BeLessThanOrEqualTo(128);
        run.Candidates.Should().ContainSingle();
    }

    [Fact]
    public async Task MissingContextAbstainsWithoutCallingTheChatProvider()
    {
        var fixture = await Fixture.CreateAsync();
        var empty = fixture.Profile with { ChunkIds = [], DocumentIds = [], ChunkCount = 0, DocumentCount = 0 };
        await fixture.State.SaveAsync("profiles", empty.Id, empty);
        var run = await fixture.Pipeline.QueryAsync(fixture.Request());
        run.Abstained.Should().BeTrue();
        run.Citations.Should().BeEmpty();
        run.Context.Should().BeEmpty();
        fixture.Chat.Calls.Should().Be(0);
    }

    [Fact]
    public async Task NumericCitationsResolveToActualChunksAndInvalidReferencesKeepTheirOrder()
    {
        var fixture = await Fixture.CreateAsync("An unsupported claim [missing], then supported content [2].");
        var run = await fixture.Pipeline.QueryAsync(fixture.Request());
        run.InvalidCitations.Should().Equal("missing");
        run.Citations.Should().HaveCount(2);
        run.Citations[0].Valid.Should().BeFalse();
        run.Citations[1].ChunkId.Should().Be(run.Context[1].Id);
        run.Answer.Should().Contain($"[{run.Context[1].Id}]").And.NotContain("[2]");
        var question = new LiveEvaluationQuestion("q", "API credential", "guide.md", [new EvaluationAnchor("API credential")]);
        var scored = WorkbenchEvaluation.Score(question, run);
        scored.Metrics.CitationAccuracy.Should().Be(0);
        scored.Metrics.CitationPrecision.Should().Be(0.5);
    }

    [Fact]
    public async Task DeterministicLiveAnswersExtractActualEvidenceAndEmitAValidCitation()
    {
        var fixture = await Fixture.CreateAsync(provider: "deterministic");
        var run = await fixture.Pipeline.QueryAsync(fixture.Request());
        run.Citations.Should().ContainSingle().Which.Valid.Should().BeTrue();
        run.Answer.Should().Contain("Refunds are available within thirty days.").And.Contain("[c1]");
        fixture.Chat.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DeterministicAnswersSelectSubstantiveEvidenceInsteadOfParsedMarkdownHeadings()
    {
        var fixture = await Fixture.CreateAsync(provider: "deterministic");
        var chunks = await fixture.Catalog.GetChunksAsync(fixture.Profile.Id);
        await fixture.Documents.UpsertChunksAsync([chunks[0] with { Text = "Client policy fixture\n\nRefunds are available within thirty days.\n\nEscalation requires contacting the account team." }]);
        var run = await fixture.Pipeline.QueryAsync(fixture.Request() with { Question = "What is the refund policy?", TopK = 1 });
        run.Answer.Should().Be("Refunds are available within thirty days. [c1]");
        run.Context[0].Content.Should().Contain("Refunds are available within thirty days.");
        run.Citations.Should().ContainSingle().Which.ChunkId.Should().Be("c1");
    }

    [Fact]
    public async Task CancellationPropagatesThroughStagesWithoutFabricatingACompletedRun()
    {
        var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var stages = new List<DetailedStage>();
        var action = async () => await fixture.Pipeline.QueryAsync(fixture.Request(), (stage, _) =>
        {
            stages.Add(stage);
            if (stage.Id == "embedding" && stage.Status == "running") { cancellation.Cancel(); }
            return Task.CompletedTask;
        }, cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        stages.Should().NotContain(stage => stage.Id == "generation");
        fixture.Chat.Calls.Should().Be(0);
    }

    [Fact]
    public async Task EvaluationRejectsMissingEvidenceBeforeCallingAnyModel()
    {
        var fixture = await Fixture.CreateAsync();
        var ingestor = new WorkbenchIngestor(new EmptySourceResolver(), new EmptyParser(), fixture.Embeddings, fixture.Documents, fixture.VectorStores, fixture.Catalog);
        using var jobs = new WorkbenchJobs(fixture.Catalog, ingestor, fixture.Pipeline, NullLogger<WorkbenchJobs>.Instance);
        var action = async () => await jobs.EnqueueEvaluationAsync(new LiveEvaluationRequest([fixture.Profile.Id], [new LiveEvaluationQuestion("q", "Question", "missing.md", [new EvaluationAnchor("missing phrase")])]));
        await action.Should().ThrowAsync<ArgumentException>();
        fixture.Embeddings.Calls.Should().Be(0);
        fixture.Chat.Calls.Should().Be(0);
    }

    [Fact]
    public async Task IngestionIsolatesTwoProfilesAndCountsRealEmbeddingOperations()
    {
        var fixture = await Fixture.CreateAsync();
        var first = await fixture.Catalog.CreateProfileAsync(fixture.Corpus.Id, new CreateProfileRequest("First", "fixed", 200, 20, "test", 2));
        var second = await fixture.Catalog.CreateProfileAsync(fixture.Corpus.Id, new CreateProfileRequest("Second", "recursive", 300, 0, "test", 2));
        var ingestor = new WorkbenchIngestor(new SingleSource(), new SingleParser(), fixture.Embeddings, fixture.Documents, fixture.VectorStores, fixture.Catalog);
        foreach (var profile in new[] { first, second })
        {
            var job = new WorkbenchJob(Guid.NewGuid().ToString("N"), "ingestion", "running", profile.Id, 0, 1, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "");
            await ingestor.IngestAsync(job, ["fixture.txt"], _ => Task.CompletedTask, (_, _) => Task.CompletedTask, CancellationToken.None);
        }
        first = await fixture.Catalog.GetProfileAsync(first.Id);
        second = await fixture.Catalog.GetProfileAsync(second.Id);
        first.Status.Should().Be("ready");
        second.Status.Should().Be("ready");
        first.DocumentIds.Should().NotIntersectWith(second.DocumentIds);
        first.ChunkIds.Should().NotIntersectWith(second.ChunkIds);
        first.EmbeddingOperations.Should().Be(first.ChunkCount);
        second.EmbeddingOperations.Should().Be(second.ChunkCount);
        fixture.Embeddings.Calls.Should().Be(first.ChunkCount + second.ChunkCount);
        var firstChunks = await fixture.Catalog.GetChunksAsync(first.Id);
        firstChunks.Should().OnlyContain(chunk => chunk.Metadata.Attributes != null && chunk.Metadata.Attributes["profileId"] == first.Id);
        (await fixture.Catalog.GetDocumentsAsync(first.Id)).Should().ContainSingle().Which.ChunkCount.Should().Be(first.ChunkCount);
    }

    [Fact]
    public async Task ElasticsearchProfilesUseSeparateIndicesAndTheirOwnDimensions()
    {
        var fixture = await Fixture.CreateAsync();
        var handler = new IndexHandler();
        var stores = new WorkbenchVectorStores(fixture.Vectors, new Factory(handler), Options.Create(new VectorStoreOptions { Provider = "elasticsearch", Endpoint = "https://fixture.test", IndexName = "legacy", Dimensions = 1536 }), NullLoggerFactory.Instance);
        var second = fixture.Profile with { Id = "another-profile", EmbeddingDimensions = 3 };
        await stores.ForProfile(fixture.Profile).EnsureIndexAsync();
        await stores.ForProfile(second).EnsureIndexAsync();
        handler.Indices.Should().Equal($"/legacy-{fixture.Profile.Id}", "/legacy-another-profile");
        handler.Dimensions.Should().Equal(2, 3);
        handler.Indices.Should().NotContain("/legacy");
    }

    [Fact]
    public async Task PausedJobsPersistTheirStateAndCanResumeWithoutChangingProfileSettings()
    {
        var fixture = await Fixture.CreateAsync();
        var ingestor = new WorkbenchIngestor(new SingleSource(), new SingleParser(), fixture.Embeddings, fixture.Documents, fixture.VectorStores, fixture.Catalog);
        using var jobs = new WorkbenchJobs(fixture.Catalog, ingestor, fixture.Pipeline, NullLogger<WorkbenchJobs>.Instance);
        var job = await jobs.EnqueueIngestionAsync(fixture.Profile.Id, new WorkbenchIngestionRequest(["fixture.txt"]));
        (await jobs.ControlAsync(job.Id, "pause")).Status.Should().Be("paused");
        (await fixture.Catalog.GetProfileAsync(fixture.Profile.Id)).Status.Should().Be("paused");
        (await jobs.ControlAsync(job.Id, "resume")).Status.Should().Be("queued");
        (await jobs.ControlAsync(job.Id, "cancel")).Status.Should().Be("canceled");
        (await jobs.GetAsync(job.Id))!.Status.Should().Be("canceled");
        (await fixture.Catalog.GetProfileAsync(fixture.Profile.Id)).ChunkSize.Should().Be(fixture.Profile.ChunkSize);
    }

    [Fact]
    public async Task FileCatalogPersistsAcrossInstancesAndRejectsPathTraversal()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rag-workbench-test-{Guid.NewGuid():N}");
        try
        {
            var options = Options.Create(new DocumentStoreOptions { LocalPath = path });
            var first = new FileWorkbenchStateStore(options);
            var corpus = new WorkbenchCorpus("corpus", "Fixture", "Persistent", DateTimeOffset.UtcNow);
            await first.SaveAsync("corpora", corpus.Id, corpus);
            var second = new FileWorkbenchStateStore(options);
            (await second.GetAsync<WorkbenchCorpus>("corpora", corpus.Id)).Should().Be(corpus);
            var action = async () => await second.GetAsync<WorkbenchCorpus>("corpora", "../escape");
            await action.Should().ThrowAsync<ArgumentException>();
        }
        finally { if (Directory.Exists(path)) { Directory.Delete(path, true); } }
    }

    [Fact]
    public async Task HttpRerankerSendsConfiguredRequestAndValidatesProviderResultIndices()
    {
        var handler = new FixtureHandler("""{"results":[{"index":1,"relevance_score":0.9},{"index":0,"relevance_score":0.1}]}""");
        var reranker = new HttpRerankerClient(new Factory(handler), Options.Create(new RerankerOptions { Endpoint = "https://fixture.test/rerank", Model = "model" }));
        var result = await reranker.RerankAsync("refund", ["Refund policy", "API access"]);
        result[1].Should().Be(0.9);
        using var request = JsonDocument.Parse(handler.Body);
        request.RootElement.GetProperty("query").GetString().Should().Be("refund");
        request.RootElement.GetProperty("documents").GetArrayLength().Should().Be(2);
        handler.Response = """{"results":[{"index":8,"relevance_score":0.9}]}""";
        var invalid = async () => await reranker.RerankAsync("refund", ["Refund policy"]);
        await invalid.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task HttpChatUsageRetainsProviderCountsAndPreservesLegacyTextContract()
    {
        var handler = new FixtureHandler("""{"choices":[{"message":{"content":"Supported [c1]"}}],"usage":{"prompt_tokens":24,"completion_tokens":7,"total_tokens":31}}""");
        var client = new HttpLlmClient(new Factory(handler), Options.Create(new LlmOptions { ChatEndpoint = "https://fixture.test/chat" }), NullLogger<HttpLlmClient>.Instance);
        var result = await client.CompleteDetailedAsync([new ChatMessage("user", "Test")]);
        result.Should().Be(new ChatCompletionResult("Supported [c1]", 24, 7, 31));
        (await client.CompleteAsync([new ChatMessage("user", "Test")])).Should().Be(result.Text);
    }

    [Theory]
    [InlineData(0, "vector", 0.7, 4096)]
    [InlineData(101, "vector", 0.7, 4096)]
    [InlineData(5, "unknown", 0.7, 4096)]
    [InlineData(5, "vector", double.NaN, 4096)]
    [InlineData(5, "vector", 1.1, 4096)]
    [InlineData(5, "vector", 0.7, 127)]
    public void DetailedQueryRejectsInvalidControls(int topK, string mode, double threshold, int budget)
    {
        var action = () => DetailedQueryPipeline.Validate(new DetailedQueryRequest("Question", "corpus", "profile", topK, mode, MinRelevance: threshold, MaxContextTokens: budget));
        action.Should().Throw<ArgumentException>();
    }

    private sealed class Fixture
    {
        public required InMemoryWorkbenchStateStore State { get; init; }
        public required InMemoryDocumentStore Documents { get; init; }
        public required InMemoryVectorStore Vectors { get; init; }
        public required WorkbenchVectorStores VectorStores { get; init; }
        public required WorkbenchCatalog Catalog { get; init; }
        public required DetailedQueryPipeline Pipeline { get; init; }
        public required WorkbenchCorpus Corpus { get; init; }
        public required IndexProfile Profile { get; init; }
        public required EmbeddingClient Embeddings { get; init; }
        public required ChatClient Chat { get; init; }
        public DetailedQueryRequest Request() => new("Refund policy", Corpus.Id, Profile.Id, TopK: 3, MinRelevance: 0);
        public static async Task<Fixture> CreateAsync(string answer = "Refunds are available within thirty days. [c1]", string provider = "openai")
        {
            var state = new InMemoryWorkbenchStateStore();
            var documents = new InMemoryDocumentStore();
            var vectors = new InMemoryVectorStore();
            var llm = Options.Create(new LlmOptions { Provider = provider, EmbeddingDimensions = 2, EmbeddingModel = "test" });
            var vectorOptions = Options.Create(new VectorStoreOptions { Dimensions = 2 });
            var vectorStores = new WorkbenchVectorStores(vectors, new Factory(new FixtureHandler("{}")), vectorOptions, NullLoggerFactory.Instance);
            var catalog = new WorkbenchCatalog(state, documents, llm, vectorOptions, Options.Create(new ChunkingOptions { SemanticDistanceThreshold = 0.31 }));
            var corpus = await catalog.CreateCorpusAsync(new CreateCorpusRequest("Fixture"));
            var profile = await catalog.CreateProfileAsync(corpus.Id, new CreateProfileRequest("Main", "fixed", EmbeddingModel: provider == "deterministic" ? "deterministic" : "test", EmbeddingDimensions: 2));
            var metadata = new DocumentMetadata("doc", "file:///guide.md", "guide.md", ".md", "text/markdown", 100, DateTimeOffset.UtcNow);
            TextChunk[] chunks = [new("c1", "doc", 0, "Refunds are available within thirty days.", 0, 40, metadata), new("c2", "doc", 1, "API credential access requires authentication.", 40, 85, metadata), new("c3", "doc", 2, "Escalation is available for support tickets.", 85, 130, metadata)];
            await documents.UpsertChunksAsync(chunks);
            await vectors.UpsertAsync([new VectorRecord("c1", "doc", [1, 0], new Dictionary<string, string>()), new VectorRecord("c2", "doc", [0.6f, 0.8f], new Dictionary<string, string>()), new VectorRecord("c3", "doc", [0, 1], new Dictionary<string, string>())]);
            profile = profile with { Status = "ready", ChunkIds = chunks.Select(chunk => chunk.Id).ToArray(), DocumentIds = ["doc"], ChunkCount = 3, DocumentCount = 1 };
            await state.SaveAsync("profiles", profile.Id, profile);
            await state.SaveAsync("documents", "doc", new WorkbenchDocument("doc", profile.Id, "guide.md", string.Join("\n", chunks.Select(chunk => chunk.Text)), 3, "file:///guide.md", profile.ChunkIds));
            var embeddings = new EmbeddingClient();
            var chat = new ChatClient(answer);
            return new Fixture { State = state, Documents = documents, Vectors = vectors, VectorStores = vectorStores, Catalog = catalog, Corpus = corpus, Profile = profile, Embeddings = embeddings, Chat = chat, Pipeline = new DetailedQueryPipeline(catalog, embeddings, vectorStores, chat, new RerankerClient(), llm, vectorOptions) };
        }
    }
    private sealed class EmbeddingClient : IEmbeddingClient
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Calls++; return Task.FromResult<IReadOnlyList<float>>([1, 0]); }
    }
    private sealed class ChatClient(string answer) : IChatClient
    {
        public int Calls { get; private set; }
        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(answer); }
    }
    private sealed class RerankerClient : IRerankerClient
    {
        public Task<IReadOnlyDictionary<int, double>> RerankAsync(string question, IReadOnlyList<string> documents, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<int, double>>(Enumerable.Range(0, documents.Count).ToDictionary(index => index, index => (double)index));
    }
    private sealed class SingleSource : IDocumentSourceResolver, IDocumentSource
    {
        public string Scheme => "file";
        public bool CanRead(string sourceUri) => true;
        public IDocumentSource Resolve(string sourceUri) => this;
        public async IAsyncEnumerable<SourceItem> EnumerateAsync(string sourceUri, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield return new SourceItem("fixture.txt", "file:///fixture.txt", "file", "fixture.txt", ".txt");
        }
    }
    private sealed class SingleParser : IDocumentParserResolver
    {
        public async IAsyncEnumerable<ParsedDocument> ParseAsync(string path, IReadOnlyDictionary<string, string>? attributes = null, string? contentType = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            var text = string.Join(" ", Enumerable.Repeat("Refunds are available within thirty days.", 20));
            yield return new ParsedDocument("parsed", text, new DocumentMetadata("parsed", "fixture.txt", "fixture.txt", ".txt", "text/plain", text.Length, DateTimeOffset.UtcNow));
        }
    }
    private sealed class EmptySourceResolver : IDocumentSourceResolver { public IDocumentSource Resolve(string sourceUri) => throw new NotSupportedException(); }
    private sealed class EmptyParser : IDocumentParserResolver
    {
        public async IAsyncEnumerable<ParsedDocument> ParseAsync(string path, IReadOnlyDictionary<string, string>? attributes = null, string? contentType = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class IndexHandler : HttpMessageHandler
    {
        public List<string> Indices { get; } = [];
        public List<int> Dimensions { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get) { return new HttpResponseMessage(HttpStatusCode.NotFound); }
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Indices.Add(request.RequestUri!.AbsolutePath);
            Dimensions.Add(payload.RootElement.GetProperty("mappings").GetProperty("properties").GetProperty("vector").GetProperty("dims").GetInt32());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
    private sealed class FixtureHandler(string response) : HttpMessageHandler
    {
        public string Response { get; set; } = response;
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Response, Encoding.UTF8, "application/json") }; }
    }
}
