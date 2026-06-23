using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Pipelines;
using Rag.Core.Stores;
using Rag.Core.Vector;
using Xunit;

namespace Rag.Core.Tests;

public sealed class QueryPipelineTests
{
    [Fact]
    public async Task CitationsFollowRetrievalOrderAndCarryChunkMetadata()
    {
        var documentStore = new InMemoryDocumentStore();
        await documentStore.UpsertChunksAsync([Chunk("chunk-a", 0, "Refunds within thirty days."), Chunk("chunk-b", 1, "Shipping takes five days.")]);
        var vectorStore = new StubVectorStore([
            new VectorSearchResult("chunk-b", "doc", 0.91),
            new VectorSearchResult("chunk-a", "doc", 0.42)
        ]);
        var chat = new RecordingChatClient();
        var pipeline = Pipeline(vectorStore, documentStore, chat);

        var answer = await pipeline.QueryAsync(new QueryRequest("What is the refund policy?"));

        answer.Citations.Select(citation => citation.ChunkId).Should().Equal("chunk-b", "chunk-a");
        answer.Citations.Select(citation => citation.Score).Should().Equal(0.91, 0.42);
        answer.Citations.Select(citation => citation.ChunkIndex).Should().Equal(1, 0);
        chat.Prompt.Should().Contain("Shipping takes five days.").And.Contain("Refunds within thirty days.");
    }

    [Fact]
    public async Task MatchesMissingFromTheDocumentStoreAreDroppedFromPromptAndCitations()
    {
        var documentStore = new InMemoryDocumentStore();
        await documentStore.UpsertChunksAsync([Chunk("chunk-a", 0, "Refunds within thirty days.")]);
        var vectorStore = new StubVectorStore([
            new VectorSearchResult("chunk-missing", "doc", 0.99),
            new VectorSearchResult("chunk-a", "doc", 0.42)
        ]);
        var chat = new RecordingChatClient();
        var pipeline = Pipeline(vectorStore, documentStore, chat);

        var answer = await pipeline.QueryAsync(new QueryRequest("What is the refund policy?"));

        answer.Citations.Select(citation => citation.ChunkId).Should().Equal("chunk-a");
        chat.Prompt.Should().NotContain("chunk-missing");
    }

    [Fact]
    public async Task RequestedTopKAndFilterReachTheVectorStore()
    {
        var vectorStore = new StubVectorStore([]);
        var filter = new VectorSearchFilter(Origins: ["s3"]);

        await Pipeline(vectorStore, new InMemoryDocumentStore(), new RecordingChatClient())
            .QueryAsync(new QueryRequest("question", TopK: 9, Filter: filter));

        vectorStore.RequestedTopK.Should().Be(9);
        vectorStore.RequestedFilter.Should().BeSameAs(filter);
    }

    private static QueryPipeline Pipeline(IVectorStore vectorStore, IDocumentStore documentStore, IChatClient chat)
    {
        return new QueryPipeline(
            new ConstantEmbeddingClient(),
            chat,
            vectorStore,
            documentStore,
            Options.Create(new LlmOptions { SystemPrompt = "Ground every answer." }));
    }

    private static TextChunk Chunk(string id, int index, string text)
    {
        var metadata = new DocumentMetadata("doc", "handbook.txt", "handbook.txt", "txt", "text/plain", text.Length, DateTimeOffset.UtcNow);
        return new TextChunk(id, "doc", index, text, 0, text.Length, metadata);
    }

    private sealed class StubVectorStore(IReadOnlyList<VectorSearchResult> results) : IVectorStore
    {
        public int RequestedTopK { get; private set; }

        public VectorSearchFilter? RequestedFilter { get; private set; }

        public Task EnsureIndexAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
            IReadOnlyList<float> queryVector,
            int topK,
            VectorSearchFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            RequestedTopK = topK;
            RequestedFilter = filter;
            return Task.FromResult(results);
        }
    }

    private sealed class ConstantEmbeddingClient : IEmbeddingClient
    {
        public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float>>([1, 0, 0]);
        }
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public string Prompt { get; private set; } = string.Empty;

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            Prompt = messages.Last(message => message.Role == "user").Content;
            return Task.FromResult("answer");
        }
    }
}
