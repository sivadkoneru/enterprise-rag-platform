using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;

namespace Rag.Core.Sources;

public sealed class AzureBlobDocumentSource(IOptions<AzureBlobOptions> azureBlobOptions, IOptions<CloudSourceOptions> options)
    : CloudDocumentSource(options)
{
    public override string Scheme => "azureblob";

    protected override async Task<IReadOnlyList<string>> ListKeysAsync(string container, string prefix, CancellationToken cancellationToken)
    {
        var blobs = new List<string>();
        await foreach (var blob in Container(container).GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            blobs.Add(blob.Name);
        }

        return blobs;
    }

    protected override async Task DownloadAsync(string container, string key, string localPath, CancellationToken cancellationToken)
    {
        await Container(container).GetBlobClient(key).DownloadToAsync(localPath, cancellationToken).ConfigureAwait(false);
    }

    protected override IEnumerable<KeyValuePair<string, string>> DescribeItem(string container, string key)
    {
        yield return new KeyValuePair<string, string>("container", container);
        yield return new KeyValuePair<string, string>("blob", key);
    }

    private BlobContainerClient Container(string container)
    {
        if (!string.IsNullOrWhiteSpace(azureBlobOptions.Value.ConnectionString))
        {
            return new BlobContainerClient(azureBlobOptions.Value.ConnectionString, container);
        }

        if (!string.IsNullOrWhiteSpace(azureBlobOptions.Value.ServiceUri))
        {
            return new BlobContainerClient(new Uri($"{azureBlobOptions.Value.ServiceUri.TrimEnd('/')}/{container}"));
        }

        throw new InvalidOperationException("AzureBlob:ConnectionString or AZURE_BLOB_CONNECTION_STRING is required for azureblob sources.");
    }
}
