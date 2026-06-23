using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;

namespace Rag.Core.Sources;

public sealed class AwsS3DocumentSource(IAmazonS3 s3Client, IOptions<CloudSourceOptions> options)
    : CloudDocumentSource(options)
{
    public override string Scheme => "s3";

    protected override async Task<IReadOnlyList<string>> ListKeysAsync(string container, string prefix, CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        var keys = new List<string>();
        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = container,
                Prefix = prefix,
                ContinuationToken = continuationToken
            }, cancellationToken).ConfigureAwait(false);

            keys.AddRange(response.S3Objects.Select(item => item.Key));
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrEmpty(continuationToken));

        return keys;
    }

    protected override async Task DownloadAsync(string container, string key, string localPath, CancellationToken cancellationToken)
    {
        using var result = await s3Client.GetObjectAsync(container, key, cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(localPath);
        await result.ResponseStream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    protected override IEnumerable<KeyValuePair<string, string>> DescribeItem(string container, string key)
    {
        yield return new KeyValuePair<string, string>("bucket", container);
        yield return new KeyValuePair<string, string>("key", key);
    }
}
