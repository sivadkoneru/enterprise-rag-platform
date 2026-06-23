using System.Text;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Pipelines;

public sealed class QueryPipeline(
    IEmbeddingClient embeddingClient,
    IChatClient chatClient,
    IVectorStore vectorStore,
    IDocumentStore documentStore,
    IOptions<LlmOptions> llmOptions) : IQueryPipeline
{
    public async Task<RagAnswer> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        var queryVector = await embeddingClient.EmbedAsync(request.Question, cancellationToken).ConfigureAwait(false);
        var matches = await vectorStore.SearchAsync(queryVector, request.TopK, request.Filter, cancellationToken).ConfigureAwait(false);
        var chunks = await documentStore.GetChunksAsync(matches.Select(match => match.ChunkId).ToArray(), cancellationToken).ConfigureAwait(false);
        var chunkById = chunks.ToDictionary(chunk => chunk.Id, StringComparer.Ordinal);
        var orderedChunks = matches.Where(match => chunkById.ContainsKey(match.ChunkId)).Select(match => chunkById[match.ChunkId]).ToArray();

        var prompt = BuildPrompt(request.Question, orderedChunks);
        var answer = await chatClient.CompleteAsync(
            [new ChatMessage("system", llmOptions.Value.SystemPrompt), new ChatMessage("user", prompt)],
            cancellationToken).ConfigureAwait(false);

        var citations = matches
            .Where(match => chunkById.ContainsKey(match.ChunkId))
            .Select(match =>
            {
                var chunk = chunkById[match.ChunkId];
                return new SourceCitation(chunk.DocumentId, chunk.Id, chunk.Metadata.Source, chunk.Index, match.Score);
            })
            .ToArray();

        return new RagAnswer(answer, citations);
    }

    private static string BuildPrompt(string question, IReadOnlyList<TextChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Question: {question}");
        builder.AppendLine("Context:");
        foreach (var chunk in chunks)
        {
            builder.AppendLine($"[{chunk.Id}] source={chunk.Metadata.Source} origin={chunk.Metadata.Origin} type=.{chunk.Metadata.Extension}");
            builder.AppendLine(chunk.Text);
        }

        return builder.ToString();
    }
}
