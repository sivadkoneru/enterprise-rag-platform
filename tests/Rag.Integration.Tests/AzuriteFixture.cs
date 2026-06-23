using Azure.Storage.Blobs;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Rag.Integration.Tests;

/// <summary>
/// Starts a single Azurite blob endpoint for the whole test class, using the well-known local
/// development account that Azurite ships with.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string AccountName = "devstoreaccount1";
    private const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private IContainer? _container;

    public string ConnectionString =>
        $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};" +
        $"BlobEndpoint=http://127.0.0.1:{Container.GetMappedPublicPort(10000)}/{AccountName};";

    private IContainer Container =>
        _container ?? throw new InvalidOperationException("AzuriteFixture was not initialized.");

    // Building the container is deferred to InitializeAsync (rather than a field initializer) so
    // that Docker-endpoint resolution failures are caught by DockerPrerequisite.StartAsync below
    // instead of throwing out of the constructor.
    public async Task InitializeAsync()
    {
        _container = await DockerPrerequisite.StartAsync(() => new ContainerBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:3.36.0")
            .WithCommand("azurite-blob", "--blobHost", "0.0.0.0")
            .WithPortBinding(10000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
            .Build());
    }

    public Task DisposeAsync()
    {
        return _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
    }

    /// <summary>Creates a uniquely named container seeded with the supplied blob name/content pairs.</summary>
    public async Task<string> SeedContainerAsync(IReadOnlyDictionary<string, string> blobs)
    {
        var name = $"rag-docs-{Guid.NewGuid():N}";
        var container = new BlobContainerClient(ConnectionString, name);
        await container.CreateIfNotExistsAsync();

        foreach (var (blobName, content) in blobs)
        {
            await container.GetBlobClient(blobName).UploadAsync(BinaryData.FromString(content), overwrite: true);
        }

        return name;
    }
}
