using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
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
    IOptions<LlmOptions> llmOptions,
    ILogger<QueryPipeline> logger) : IQueryPipeline
{
    public async Task<RagAnswer> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        var queryVector = await embeddingClient.EmbedAsync(request.Question, cancellationToken).ConfigureAwait(false);
        var matches = await vectorStore.SearchAsync(queryVector, request.TopK, request.Filter, cancellationToken).ConfigureAwait(false);
        logger.LogDebug(
            "Query executed with {TopK} requested and {MatchCount} vector match(es) returned.",
            request.TopK,
            matches.Count);
        var chunks = await documentStore.GetChunksAsync(matches.Select(match => match.ChunkId).ToArray(), cancellationToken).ConfigureAwait(false);

        // A chunk can be missing when the vector index is ahead of the document store; those
        // matches are dropped so the prompt and the citations always describe the same context.
        var chunkById = new Dictionary<string, TextChunk>(StringComparer.Ordinal);
        foreach (var chunk in chunks)
        {
            chunkById[chunk.Id] = chunk;
        }

        var orderedChunks = new List<TextChunk>(matches.Count);
        var citations = new List<SourceCitation>(matches.Count);
        foreach (var match in matches)
        {
            if (!chunkById.TryGetValue(match.ChunkId, out var chunk))
            {
                logger.LogWarning(
                    "Dropped vector match for chunk {ChunkId}; the chunk was not found in the document store.",
                    match.ChunkId);
                continue;
            }

            orderedChunks.Add(chunk);
            citations.Add(new SourceCitation(chunk.DocumentId, chunk.Id, chunk.Metadata.Source, chunk.Index, match.Score));
        }

        var prompt = BuildPrompt(request.Question, orderedChunks);
        var answer = await chatClient.CompleteAsync(
            [new ChatMessage("system", llmOptions.Value.SystemPrompt), new ChatMessage("user", prompt)],
            cancellationToken).ConfigureAwait(false);

        return new RagAnswer(answer, citations);
    }

    private static string BuildPrompt(string question, IReadOnlyList<TextChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Question: {question}");
        builder.AppendLine("Context:");
        foreach (var chunk in chunks)
        {
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"[{chunk.Id}] source={chunk.Metadata.Source} origin={chunk.Metadata.Origin} type={FileTypes.Normalize(chunk.Metadata.Extension)}");
            builder.AppendLine(chunk.Text);
        }

        return builder.ToString();
    }
}
