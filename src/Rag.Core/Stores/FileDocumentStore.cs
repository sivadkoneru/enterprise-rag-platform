using System.Text.Json;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Stores;

public class FileDocumentStore(IOptions<DocumentStoreOptions> options) : IDocumentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    protected virtual string StoreName => "file";

    public async Task UpsertDocumentAsync(ParsedDocument document, CancellationToken cancellationToken = default)
    {
        var root = Root();
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        await WriteAsync(Path.Combine(root, "documents", $"{document.Id}.json"), document, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertChunksAsync(IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken = default)
    {
        var root = Root();
        Directory.CreateDirectory(Path.Combine(root, "chunks"));
        foreach (var chunk in chunks)
        {
            await WriteAsync(Path.Combine(root, "chunks", $"{SafeName(chunk.Id)}.json"), chunk, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<TextChunk>> GetChunksAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        var root = Root();
        var chunks = new List<TextChunk>();
        foreach (var id in chunkIds)
        {
            var path = Path.Combine(root, "chunks", $"{SafeName(id)}.json");
            if (!File.Exists(path))
            {
                continue;
            }

            await using var stream = File.OpenRead(path);
            var chunk = await JsonSerializer.DeserializeAsync<TextChunk>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (chunk is not null)
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    protected string Root()
    {
        return Path.Combine(options.Value.LocalPath, StoreName);
    }

    private static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string SafeName(string id)
    {
        return string.Join('_', id.Split(Path.GetInvalidFileNameChars()));
    }
}
