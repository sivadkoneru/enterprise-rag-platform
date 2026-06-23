using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Parsing;

namespace Rag.Core.Sources;

public sealed class AzureBlobDocumentSource(IOptions<AzureBlobOptions> options) : IDocumentSource
{
    public string Scheme => "azureblob";

    public bool CanRead(string sourceUri)
    {
        return Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<SourceItem> EnumerateAsync(
        string sourceUri,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parsed = Parse(sourceUri);
        var container = Container(parsed.Container);
        var blobs = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(prefix: parsed.Prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            blobs.Add(blob.Name);
        }

        var blobNames = blobs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var blobName in blobs.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!DocumentSourceSupport.IsSupported(blobName))
            {
                continue;
            }

            var localPath = DocumentSourceSupport.TempPathFor(blobName);
            var client = container.GetBlobClient(blobName);
            await client.DownloadToAsync(localPath, cancellationToken).ConfigureAwait(false);

            var attributes = new Dictionary<string, string>
            {
                ["container"] = parsed.Container,
                ["blob"] = blobName,
                [StructuredSchemaLoader.SourceFileNameAttribute] = Path.GetFileName(blobName)
            };
            var cleanupPaths = new List<string> { localPath };
            var schemaName = SchemaName(blobName, blobNames);
            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                var schemaPath = DocumentSourceSupport.TempPathFor(schemaName);
                await container.GetBlobClient(schemaName).DownloadToAsync(schemaPath, cancellationToken).ConfigureAwait(false);
                attributes[StructuredSchemaLoader.SchemaPathAttribute] = schemaPath;
                cleanupPaths.Add(schemaPath);
            }

            yield return new SourceItem(
                localPath,
                $"azureblob://{parsed.Container}/{blobName}",
                Scheme,
                Path.GetFileName(blobName),
                DocumentSourceSupport.NormalizeExtension(blobName),
                attributes,
                () => CleanupAsync(cleanupPaths));
        }
    }

    private BlobContainerClient Container(string container)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.ConnectionString))
        {
            return new BlobContainerClient(options.Value.ConnectionString, container);
        }

        if (!string.IsNullOrWhiteSpace(options.Value.ServiceUri))
        {
            return new BlobContainerClient(new Uri($"{options.Value.ServiceUri.TrimEnd('/')}/{container}"));
        }

        throw new InvalidOperationException("AzureBlob:ConnectionString or AZURE_BLOB_CONNECTION_STRING is required for azureblob sources.");
    }

    private static (string Container, string Prefix) Parse(string sourceUri)
    {
        var uri = new Uri(sourceUri, UriKind.Absolute);
        return (uri.Host, uri.AbsolutePath.TrimStart('/'));
    }

    private static string? SchemaName(string blobName, ISet<string> blobNames)
    {
        return StructuredSchemaLoader.CandidateSchemaNames(blobName)
            .FirstOrDefault(blobNames.Contains);
    }

    private static async ValueTask CleanupAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            await DocumentSourceSupport.CleanupTempFileAsync(path).ConfigureAwait(false);
        }
    }
}
