using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Sources;
using Xunit;

namespace Rag.Core.Tests;

public sealed class DocumentSourceResolverTests
{
    [Theory]
    [InlineData("./samples", typeof(LocalDirectorySource))]
    [InlineData("/absolute/path/handbook.txt", typeof(LocalDirectorySource))]
    [InlineData("file:///docs/handbook.txt", typeof(LocalDirectorySource))]
    [InlineData("s3://rag-docs/policies", typeof(AwsS3DocumentSource))]
    [InlineData("S3://rag-docs/policies", typeof(AwsS3DocumentSource))]
    [InlineData("azureblob://rag-docs/policies", typeof(AzureBlobDocumentSource))]
    [InlineData("AzureBlob://rag-docs/policies", typeof(AzureBlobDocumentSource))]
    public void SourceUriSchemeSelectsTheMatchingAdapter(string sourceUri, Type expected)
    {
        Resolver().Resolve(sourceUri).Should().BeOfType(expected);
    }

    [Theory]
    [InlineData("gs://rag-docs/policies")]
    [InlineData("https://example.test/handbook.txt")]
    public void UnsupportedSchemesAreRejected(string sourceUri)
    {
        var act = () => Resolver().Resolve(sourceUri);

        act.Should().Throw<NotSupportedException>().WithMessage($"*{sourceUri}*");
    }

    private static IDocumentSourceResolver Resolver()
    {
        // Explicit dummy credentials keep client construction off the ambient AWS credential
        // chain; resolution only consults CanRead and never calls the service.
        var s3 = new AmazonS3Client(
            new BasicAWSCredentials("test", "test"),
            new AmazonS3Config { RegionEndpoint = RegionEndpoint.USEast1, ForcePathStyle = true });

        return new DocumentSourceResolver(
        [
            new LocalDirectorySource(Options.Create(new LocalSourceOptions())),
            new AwsS3DocumentSource(s3, Options.Create(new CloudSourceOptions())),
            new AzureBlobDocumentSource(Options.Create(new AzureBlobOptions()), Options.Create(new CloudSourceOptions()))
        ]);
    }
}
