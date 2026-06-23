using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Rag.Integration.Tests;

/// <summary>
/// Starts a single LocalStack S3 endpoint for the whole test class. Credentials are supplied
/// explicitly so the tests never depend on an ambient AWS profile.
/// </summary>
public sealed class LocalStackFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string ServiceUrl => $"http://localhost:{Container.GetMappedPublicPort(4566)}";

    private IContainer Container =>
        _container ?? throw new InvalidOperationException("LocalStackFixture was not initialized.");

    // Building the container is deferred to InitializeAsync (rather than a field initializer) so
    // that Docker-endpoint resolution failures are caught by DockerPrerequisite.StartAsync below
    // instead of throwing out of the constructor.
    public async Task InitializeAsync()
    {
        _container = await DockerPrerequisite.StartAsync(() => new ContainerBuilder()
            .WithImage("localstack/localstack:3")
            .WithEnvironment("SERVICES", "s3")
            .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
            .WithPortBinding(4566, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(4566).ForPath("/_localstack/health")))
            .Build());
    }

    public Task DisposeAsync()
    {
        return _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
    }

    public IAmazonS3 CreateClient()
    {
        return new AmazonS3Client(
            new BasicAWSCredentials("test", "test"),
            new AmazonS3Config
            {
                ServiceURL = ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            });
    }

    /// <summary>Creates a uniquely named bucket seeded with the supplied key/content pairs.</summary>
    public async Task<string> SeedBucketAsync(IReadOnlyDictionary<string, string> objects)
    {
        var bucket = $"rag-docs-{Guid.NewGuid():N}";
        using var client = CreateClient();
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });

        foreach (var (key, content) in objects)
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = content
            });
        }

        return bucket;
    }
}
