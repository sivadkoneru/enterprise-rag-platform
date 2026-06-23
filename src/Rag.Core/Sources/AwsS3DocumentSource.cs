using Amazon.S3;
using Amazon.S3.Model;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Sources;

public sealed class AwsS3DocumentSource(IAmazonS3 s3Client) : IDocumentSource
{
    public string Scheme => "s3";

    public bool CanRead(string sourceUri)
    {
        return Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<SourceItem> EnumerateAsync(
        string sourceUri,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var uri = Parse(sourceUri);
        string? continuationToken = null;
        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = uri.Bucket,
                Prefix = uri.Prefix,
                ContinuationToken = continuationToken
            }, cancellationToken).ConfigureAwait(false);

            foreach (var entry in response.S3Objects
                .Where(item => DocumentSourceSupport.IsSupported(item.Key))
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var localPath = DocumentSourceSupport.TempPathFor(entry.Key);
                using (var result = await s3Client.GetObjectAsync(uri.Bucket, entry.Key, cancellationToken).ConfigureAwait(false))
                await using (var target = File.Create(localPath))
                {
                    await result.ResponseStream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                }

                yield return new SourceItem(
                    localPath,
                    $"s3://{uri.Bucket}/{entry.Key}",
                    Scheme,
                    Path.GetFileName(entry.Key),
                    DocumentSourceSupport.NormalizeExtension(entry.Key),
                    new Dictionary<string, string>
                    {
                        ["bucket"] = uri.Bucket,
                        ["key"] = entry.Key
                    },
                    () => DocumentSourceSupport.CleanupTempFileAsync(localPath));
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrEmpty(continuationToken));
    }

    private static (string Bucket, string Prefix) Parse(string sourceUri)
    {
        var uri = new Uri(sourceUri, UriKind.Absolute);
        return (uri.Host, uri.AbsolutePath.TrimStart('/'));
    }
}
