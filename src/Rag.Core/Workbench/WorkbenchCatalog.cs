using System.Text.Json;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Chunking;
using Rag.Core.Common;
using Rag.Core.Configuration;
using Rag.Core.Models;

namespace Rag.Core.Workbench;

public sealed class WorkbenchCatalog(IWorkbenchStateStore store, IDocumentStore documents, IOptions<LlmOptions> llm, IOptions<VectorStoreOptions> vectors, IOptions<ChunkingOptions> chunking)
{
    public IWorkbenchStateStore Store => store;
    public Task<IReadOnlyList<WorkbenchCorpus>> ListCorporaAsync(CancellationToken token = default) => store.ListAsync<WorkbenchCorpus>("corpora", token);
    public async Task<WorkbenchCorpus> CreateCorpusAsync(CreateCorpusRequest request, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 || request.Description is null || request.Description.Length > 2000)
        {
            throw new ArgumentException("Provide a corpus name up to 100 characters and description up to 2,000 characters.");
        }

        var corpus = new WorkbenchCorpus(Guid.NewGuid().ToString("N"), request.Name.Trim(), request.Description.Trim(), DateTimeOffset.UtcNow);
        await store.SaveAsync("corpora", corpus.Id, corpus, token).ConfigureAwait(false);
        return corpus;
    }
    public async Task<IReadOnlyList<IndexProfile>> ListProfilesAsync(string corpusId, CancellationToken token = default)
    {
        return (await store.ListAsync<IndexProfile>("profiles", token).ConfigureAwait(false)).Where(profile => profile.CorpusId == corpusId).ToArray();
    }
    public async Task<IndexProfile> CreateProfileAsync(string corpusId, CreateProfileRequest request, CancellationToken token = default)
    {
        if (await store.GetAsync<WorkbenchCorpus>("corpora", corpusId, token).ConfigureAwait(false) is null)
        {
            throw new KeyNotFoundException("Corpus not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 || request.Strategy is not ("fixed" or "recursive" or "markdown-aware" or "semantic") || request.ChunkSize is < 200 or > 16000 || request.ChunkOverlap < 0 || request.ChunkOverlap >= request.ChunkSize)
        {
            throw new ArgumentException("Invalid indexing profile name, strategy, chunk size, or overlap.");
        }

        if (request.EmbeddingDimensions != llm.Value.EmbeddingDimensions)
        {
            throw new ArgumentException("Profile dimensions must match the configured embedding endpoint dimensions.");
        }

        var activeModel = WorkbenchModelIdentity.EmbeddingModel(llm.Value);
        if (string.IsNullOrWhiteSpace(activeModel)) { throw new ArgumentException("Configure the HTTP embedding model before creating an indexing profile."); }
        var model = string.IsNullOrWhiteSpace(request.EmbeddingModel) ? activeModel : request.EmbeddingModel.Trim();
        if (model != activeModel)
        {
            throw new ArgumentException("Profile model must match the configured embedding endpoint model.");
        }

        var id = Guid.NewGuid().ToString("N");
        var profile = new IndexProfile(id, corpusId, request.Name.Trim(), request.Strategy, request.ChunkSize, request.ChunkOverlap, model, request.EmbeddingDimensions, "empty", 0, 0, DateTimeOffset.UtcNow, [], []) { SemanticDistanceThreshold = chunking.Value.SemanticDistanceThreshold, VectorIndexName = vectors.Value.Provider.Equals("elasticsearch", StringComparison.OrdinalIgnoreCase) ? $"{vectors.Value.IndexName}-{id}" : null };
        await store.SaveAsync("profiles", profile.Id, profile, token).ConfigureAwait(false);
        return profile;
    }
    public async Task<IndexProfile> GetProfileAsync(string id, CancellationToken token = default) => await store.GetAsync<IndexProfile>("profiles", id, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Profile not found.");
    public async Task<IReadOnlyList<WorkbenchDocument>> GetDocumentsAsync(string profileId, CancellationToken token = default)
    {
        var profile = await GetProfileAsync(profileId, token).ConfigureAwait(false);
        var results = new List<WorkbenchDocument>();
        foreach (var id in profile.DocumentIds)
        {
            var document = await store.GetAsync<WorkbenchDocument>("documents", id, token).ConfigureAwait(false);
            if (document is not null && document.ProfileId == profileId)
            {
                results.Add(document);
            }
        }
        return results;
    }
    public async Task<IReadOnlyList<TextChunk>> GetChunksAsync(string profileId, string? documentId = null, CancellationToken token = default)
    {
        var profile = await GetProfileAsync(profileId, token).ConfigureAwait(false);
        if (profile.ChunkIds.Count == 0)
        {
            return [];
        }

        var result = await documents.GetChunksAsync(profile.ChunkIds, token).ConfigureAwait(false);
        return result.Where(chunk => profile.DocumentIds.Contains(chunk.DocumentId) && (documentId is null || chunk.DocumentId == documentId)).ToArray();
    }
    public static PageResult<T> Page<T>(IReadOnlyList<T> values, int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 200)
        {
            throw new ArgumentException("Pagination offset must be nonnegative and limit between 1 and 200.");
        }

        return new PageResult<T>(values.Skip(offset).Take(limit).ToArray(), values.Count, offset, limit);
    }
}

public sealed class WorkbenchIngestor(IDocumentSourceResolver sources, IDocumentParserResolver parsers, IEmbeddingClient embeddings, IDocumentStore documents, WorkbenchVectorStores vectorStores, WorkbenchCatalog catalog)
{
    public async Task IngestAsync(WorkbenchJob job, IReadOnlyList<string> sourceUris, Func<CancellationToken, Task> checkControl, Func<int, CancellationToken, Task> reportProgress, CancellationToken token)
    {
        if (job.ProfileId is null)
        {
            throw new ArgumentException("An indexing profile is required.");
        }

        var profile = await catalog.GetProfileAsync(job.ProfileId, token).ConfigureAwait(false);
        var vectors = vectorStores.ForProfile(profile);
        var options = Options.Create(new ChunkingOptions { Size = profile.ChunkSize, Overlap = profile.ChunkOverlap, SemanticDistanceThreshold = profile.SemanticDistanceThreshold });
        var counted = new CountingEmbeddingClient(embeddings);
        IChunkingStrategy strategy = profile.Strategy switch
        {
            "fixed" => new FixedChunkingStrategy(options),
            "recursive" => new RecursiveChunkingStrategy(options),
            "markdown-aware" => new MarkdownAwareChunkingStrategy(options),
            "semantic" => new SemanticChunkingStrategy(counted, options),
            _ => throw new ArgumentException("Unsupported indexing strategy.")
        };
        await vectors.EnsureIndexAsync(token).ConfigureAwait(false);
        profile = profile with { Status = "indexing" };
        await catalog.Store.SaveAsync("profiles", profile.Id, profile, token).ConfigureAwait(false);
        var completed = 0;
        foreach (var uri in sourceUris)
        {
            await checkControl(token).ConfigureAwait(false);
            await foreach (var item in sources.Resolve(uri).EnumerateAsync(uri, token).ConfigureAwait(false))
            {
                await using (item.ConfigureAwait(false))
                {
                    await foreach (var parsed in parsers.ParseAsync(item.LocalPath, item.Attributes, cancellationToken: token).ConfigureAwait(false))
                    {
                        await checkControl(token).ConfigureAwait(false);
                        var recordKey = parsed.Metadata.Attributes?.GetValueOrDefault("recordKey") ?? parsed.Id;
                        var id = StableId.Compute($"workbench:{profile.Id}:{item.Origin}:{item.Source}:{recordKey}");
                        var attributes = new Dictionary<string, string>(parsed.Metadata.Attributes ?? new Dictionary<string, string>()) { ["profileId"] = profile.Id, ["corpusId"] = profile.CorpusId };
                        var metadata = parsed.Metadata with { DocumentId = id, FileName = item.FileName, Source = item.Source, Origin = item.Origin, Attributes = attributes };
                        var document = new ParsedDocument(id, parsed.Text, metadata);
                        var chunks = await strategy.ChunkAsync(document, token).ConfigureAwait(false);
                        var records = new List<VectorRecord>();
                        foreach (var chunk in chunks)
                        {
                            await checkControl(token).ConfigureAwait(false);
                            var vector = await counted.EmbedAsync(chunk.Text, token).ConfigureAwait(false);
                            if (vector.Count != profile.EmbeddingDimensions)
                            {
                                throw new InvalidDataException("Embedding provider returned dimensions that differ from the indexing profile.");
                            }

                            records.Add(new VectorRecord(chunk.Id, id, vector, new Dictionary<string, string> { ["source"] = item.Source, ["origin"] = item.Origin, ["fileName"] = item.FileName, ["fileType"] = FileTypes.Normalize(item.Extension), ["profileId"] = profile.Id, ["corpusId"] = profile.CorpusId, ["text"] = chunk.Text }));
                        }
                        await documents.UpsertDocumentAsync(document, token).ConfigureAwait(false);
                        await documents.UpsertChunksAsync(chunks, token).ConfigureAwait(false);
                        await vectors.UpsertAsync(records, token).ConfigureAwait(false);
                        var oldDocument = await catalog.Store.GetAsync<WorkbenchDocument>("documents", id, token).ConfigureAwait(false);
                        await catalog.Store.SaveAsync("documents", id, new WorkbenchDocument(id, profile.Id, item.FileName, document.Text, chunks.Count, item.Source, chunks.Select(chunk => chunk.Id).ToArray()), token).ConfigureAwait(false);
                        var documentIds = profile.DocumentIds.Append(id).Distinct(StringComparer.Ordinal).ToArray();
                        var removedIds = oldDocument?.ChunkIds ?? [];
                        var chunkIds = profile.ChunkIds.Except(removedIds).Concat(chunks.Select(chunk => chunk.Id)).Distinct(StringComparer.Ordinal).ToArray();
                        profile = profile with { DocumentIds = documentIds, ChunkIds = chunkIds, DocumentCount = documentIds.Length, ChunkCount = chunkIds.Length, EmbeddingOperations = profile.EmbeddingOperations + counted.TakeCount() };
                        await catalog.Store.SaveAsync("profiles", profile.Id, profile, token).ConfigureAwait(false);
                    }
                }
            }
            completed++;
            await reportProgress(completed, token).ConfigureAwait(false);
        }
        await catalog.Store.SaveAsync("profiles", profile.Id, profile with { Status = "ready" }, token).ConfigureAwait(false);
    }
    private sealed class CountingEmbeddingClient(IEmbeddingClient inner) : IEmbeddingClient
    {
        private int _count;
        public int TakeCount() { var count = _count; _count = 0; return count; }
        public async Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
        {
            var result = await inner.EmbedAsync(input, cancellationToken).ConfigureAwait(false);
            _count++;
            return result;
        }
    }
}
