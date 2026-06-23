using FluentAssertions;
using Xunit;

namespace Rag.Integration.Tests;

public sealed class LocalCloudComposeTests
{
    [Fact]
    public async Task DockerComposeIncludesLocalCloudSourceBackends()
    {
        var compose = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "docker-compose.yml"));

        compose.Should().Contain("localstack:");
        compose.Should().Contain("SERVICES: s3");
        compose.Should().Contain("4566:4566");
        compose.Should().Contain("azurite:");
        compose.Should().Contain("10000:10000");
    }

    [Fact]
    public async Task EnvExampleDocumentsAsyncIngestionAndGroundingSettings()
    {
        var env = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), ".env.example"));

        env.Should().Contain("LLM_SYSTEM_PROMPT=");
        env.Should().Contain("INGESTION_MAX_PARALLELISM=");
        env.Should().Contain("S3_ENDPOINT=http://localhost:4566");
        env.Should().Contain("AZURE_BLOB_CONNECTION_STRING=");
    }

    private static string RepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    }
}
