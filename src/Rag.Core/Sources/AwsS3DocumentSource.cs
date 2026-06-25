using Amazon.S3;
using Amazon.S3.Model;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Parsing;

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
        var objects = new List<S3Object>();
        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = uri.Bucket,
                Prefix = uri.Prefix,
                ContinuationToken = continuationToken
            }, cancellationToken).ConfigureAwait(false);

            objects.AddRange(response.S3Objects);
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrEmpty(continuationToken));

        var objectKeys = objects.Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in objects
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

            var attributes = new Dictionary<string, string>
            {
                ["bucket"] = uri.Bucket,
                ["key"] = entry.Key,
                [StructuredSchemaLoader.SourceFileNameAttribute] = Path.GetFileName(entry.Key)
            };
            var cleanupPaths = new List<string> { localPath };
            var schemaKey = SchemaKey(entry.Key, objectKeys);
            if (!string.IsNullOrWhiteSpace(schemaKey))
            {
                var schemaPath = DocumentSourceSupport.TempPathFor(schemaKey);
                using (var schema = await s3Client.GetObjectAsync(uri.Bucket, schemaKey, cancellationToken).ConfigureAwait(false))
                await using (var target = File.Create(schemaPath))
                {
                    await schema.ResponseStream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                }

                attributes[StructuredSchemaLoader.SchemaPathAttribute] = schemaPath;
                cleanupPaths.Add(schemaPath);
            }

            yield return new SourceItem(
                localPath,
                $"s3://{uri.Bucket}/{entry.Key}",
                Scheme,
                Path.GetFileName(entry.Key),
                DocumentSourceSupport.NormalizeExtension(entry.Key),
                attributes,
                () => CleanupAsync(cleanupPaths));
        }
    }

    private static (string Bucket, string Prefix) Parse(string sourceUri)
    {
        var uri = new Uri(sourceUri, UriKind.Absolute);
        return (uri.Host, uri.AbsolutePath.TrimStart('/'));
    }

    private static string? SchemaKey(string objectKey, ISet<string> objectKeys)
    {
        return StructuredSchemaLoader.CandidateSchemaNames(objectKey)
            .FirstOrDefault(objectKeys.Contains);
    }

    private static async ValueTask CleanupAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            await DocumentSourceSupport.CleanupTempFileAsync(path).ConfigureAwait(false);
        }
    }
}
