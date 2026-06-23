using System.Text;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Pipelines;

public sealed class QueryPipeline(
    IEmbeddingClient embeddingClient,
    IChatClient chatClient,
    IVectorStore vectorStore,
    IDocumentStore documentStore) : IQueryPipeline
{
    public async Task<RagAnswer> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        var queryVector = await embeddingClient.EmbedAsync(request.Question, cancellationToken).ConfigureAwait(false);
        var matches = await vectorStore.SearchAsync(queryVector, request.TopK, cancellationToken).ConfigureAwait(false);
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
                continue;
            }

            orderedChunks.Add(chunk);
            citations.Add(new SourceCitation(chunk.DocumentId, chunk.Id, chunk.Metadata.Source, chunk.Index, match.Score));
        }

        var prompt = BuildPrompt(request.Question, orderedChunks);
        var answer = await chatClient.CompleteAsync(
            [new ChatMessage("system", "Answer using only the supplied context and include source citations."), new ChatMessage("user", prompt)],
            cancellationToken).ConfigureAwait(false);

        return new RagAnswer(answer, citations);
    }

    private static string BuildPrompt(string question, IReadOnlyList<TextChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Question: {question}");
        builder.AppendLine("Context:");
        foreach (var chunk in chunks)
        {
            builder.AppendLine($"[{chunk.Id}] {chunk.Text}");
        }

        return builder.ToString();
    }
}
