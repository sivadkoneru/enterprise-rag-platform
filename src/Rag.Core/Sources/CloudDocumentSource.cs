using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Parsing;

namespace Rag.Core.Sources;

/// <summary>
/// Shared enumeration, sidecar-schema resolution, and allow-list enforcement for cloud-backed
/// document sources. Backends supply how to list keys, download an object, and describe it.
/// </summary>
public abstract class CloudDocumentSource(IOptions<CloudSourceOptions> options) : IDocumentSource
{
    public abstract string Scheme { get; }

    public bool CanRead(string sourceUri)
    {
        return Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<SourceItem> EnumerateAsync(
        string sourceUri,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureAllowed(sourceUri);
        var (container, prefix) = Parse(sourceUri);
        var keys = await ListKeysAsync(container, prefix, cancellationToken).ConfigureAwait(false);
        var keySet = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys
            .Where(DocumentSourceSupport.IsSupported)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localPath = DocumentSourceSupport.TempPathFor(key);
            await DownloadAsync(container, key, localPath, cancellationToken).ConfigureAwait(false);

            var attributes = new Dictionary<string, string>(DescribeItem(container, key))
            {
                [StructuredSchemaLoader.SourceFileNameAttribute] = Path.GetFileName(key)
            };
            var cleanupPaths = new List<string> { localPath };
            var schemaKey = FindSchemaKey(key, keySet);
            if (!string.IsNullOrWhiteSpace(schemaKey))
            {
                var schemaPath = DocumentSourceSupport.TempPathFor(schemaKey);
                await DownloadAsync(container, schemaKey, schemaPath, cancellationToken).ConfigureAwait(false);
                attributes[StructuredSchemaLoader.SchemaPathAttribute] = schemaPath;
                cleanupPaths.Add(schemaPath);
            }

            yield return new SourceItem(
                localPath,
                $"{Scheme}://{container}/{key}",
                Scheme,
                Path.GetFileName(key),
                DocumentSourceSupport.NormalizeExtension(key),
                attributes,
                () => CleanupAsync(cleanupPaths));
        }
    }

    /// <summary>Lists every object key under <paramref name="prefix"/> in <paramref name="container"/>.</summary>
    protected abstract Task<IReadOnlyList<string>> ListKeysAsync(string container, string prefix, CancellationToken cancellationToken);

    /// <summary>Downloads a single object to <paramref name="localPath"/>.</summary>
    protected abstract Task DownloadAsync(string container, string key, string localPath, CancellationToken cancellationToken);

    /// <summary>Backend-specific attributes recorded for the ingested item, for example bucket/key or container/blob.</summary>
    protected abstract IEnumerable<KeyValuePair<string, string>> DescribeItem(string container, string key);

    private void EnsureAllowed(string sourceUri)
    {
        var allowedPrefixes = options.Value.AllowedPrefixes;
        if (allowedPrefixes.Count == 0)
        {
            return;
        }

        if (allowedPrefixes.Any(prefix => MatchesPrefix(sourceUri, prefix)))
        {
            return;
        }

        throw new SourcePathNotAllowedException();
    }

    private static bool MatchesPrefix(string sourceUri, string prefix)
    {
        var trimmed = prefix.TrimEnd('/');
        return sourceUri.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            sourceUri.StartsWith(trimmed + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindSchemaKey(string key, ISet<string> keys)
    {
        return StructuredSchemaLoader.CandidateSchemaNames(key).FirstOrDefault(keys.Contains);
    }

    private static (string Container, string Prefix) Parse(string sourceUri)
    {
        var uri = new Uri(sourceUri, UriKind.Absolute);
        return (uri.Host, uri.AbsolutePath.TrimStart('/'));
    }

    private static async ValueTask CleanupAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            await DocumentSourceSupport.CleanupTempFileAsync(path).ConfigureAwait(false);
        }
    }
}
