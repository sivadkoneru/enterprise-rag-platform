using DotNet.Testcontainers.Containers;

namespace Rag.Integration.Tests;

/// <summary>
/// Wraps Testcontainers container startup so a missing or unreachable Docker daemon surfaces one
/// explicit, actionable failure instead of a raw Testcontainers stack trace. Testcontainers can fail
/// either while resolving the daemon endpoint (synchronously, inside <c>Build</c>) or while connecting
/// to start the container, so both are covered here.
/// </summary>
internal static class DockerPrerequisite
{
    public const string Message =
        "Docker is required for Rag.Integration.Tests. Start Docker and rerun, or exclude these tests with: dotnet test --filter Category!=Integration";

    /// <summary>Builds and starts a container, translating any Docker-unreachable failure into <see cref="Message"/>.</summary>
    public static async Task<IContainer> StartAsync(Func<IContainer> build)
    {
        try
        {
            var container = build();
            await container.StartAsync();
            return container;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(Message, exception);
        }
    }
}
