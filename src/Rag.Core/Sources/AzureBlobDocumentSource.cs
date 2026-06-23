using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Models;

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
        await foreach (var blob in container.GetBlobsAsync(prefix: parsed.Prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!DocumentSourceSupport.IsSupported(blob.Name))
            {
                continue;
            }

            var localPath = DocumentSourceSupport.TempPathFor(blob.Name);
            var client = container.GetBlobClient(blob.Name);
            await client.DownloadToAsync(localPath, cancellationToken).ConfigureAwait(false);

            yield return new SourceItem(
                localPath,
                $"azureblob://{parsed.Container}/{blob.Name}",
                Scheme,
                Path.GetFileName(blob.Name),
                DocumentSourceSupport.NormalizeExtension(blob.Name),
                new Dictionary<string, string>
                {
                    ["container"] = parsed.Container,
                    ["blob"] = blob.Name
                },
                () => DocumentSourceSupport.CleanupTempFileAsync(localPath));
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
}
