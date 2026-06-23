using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Cli;
using Rag.Core.DependencyInjection;
using Xunit;

namespace Rag.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsyncReturnsNonZeroExitCodeWhenCommandFails()
    {
        using var services = BuildServices();

        var exitCode = await CliApplication.RunAsync(
            ["ingest", "/nonexistent/path/that-does-not-exist"],
            services,
            new ConfigurationBuilder().Build(),
            CancellationToken.None);

        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsyncReturnsZeroExitCodeWhenCommandSucceeds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "Refunds are available within thirty days with a receipt.");

        try
        {
            using var services = BuildServices();

            var exitCode = await CliApplication.RunAsync(
                ["ingest", path],
                services,
                new ConfigurationBuilder().Build(),
                CancellationToken.None);

            exitCode.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsyncRejectsUnrecognizedOptionInIngestArguments()
    {
        using var services = BuildServices();

        var exitCode = await CliApplication.RunAsync(
            ["ingest", "--totally-unrecognized-option"],
            services,
            new ConfigurationBuilder().Build(),
            CancellationToken.None);

        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsyncReturnsNonZeroExitCodeAndDoesNotThrowWhenCancelled()
    {
        using var services = BuildServices();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var exitCode = 0;
        var act = async () =>
        {
            exitCode = await CliApplication.RunAsync(
                ["ingest", "/some/path"],
                services,
                new ConfigurationBuilder().Build(),
                cancellationSource.Token);
        };

        await act.Should().NotThrowAsync();
        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsyncQueryWithoutFilterFlagsAppliesNoFilter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        var expectedSource = Path.GetFullPath(path);
        await File.WriteAllTextAsync(path, "Refunds are available within thirty days with a receipt.");

        try
        {
            using var services = BuildServices();
            var configuration = new ConfigurationBuilder().Build();

            var ingestExitCode = await CliApplication.RunAsync(["ingest", path], services, configuration, CancellationToken.None);
            ingestExitCode.Should().Be(0);

            var (exitCode, output) = await RunCapturingConsoleAsync(
                () => CliApplication.RunAsync(["query", "refund", "policy"], services, configuration, CancellationToken.None));

            exitCode.Should().Be(0);
            output.Should().Contain(expectedSource, "no filter flags were supplied, so the query must not exclude the ingested document");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsyncQueryWithTypeFlagAppliesFileTypesFilter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        var expectedSource = Path.GetFullPath(path);
        await File.WriteAllTextAsync(path, "Refunds are available within thirty days with a receipt.");

        try
        {
            using var services = BuildServices();
            var configuration = new ConfigurationBuilder().Build();

            var ingestExitCode = await CliApplication.RunAsync(["ingest", path], services, configuration, CancellationToken.None);
            ingestExitCode.Should().Be(0);

            var (exitCode, output) = await RunCapturingConsoleAsync(
                () => CliApplication.RunAsync(["query", "refund", "policy", "--type", "md"], services, configuration, CancellationToken.None));

            exitCode.Should().Be(0);
            output.Should().NotContain(expectedSource, "--type md must produce a FileTypes filter that excludes the ingested .txt document");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ServiceProvider BuildServices()
    {
        return new ServiceCollection()
            .AddRagPlatform(new ConfigurationBuilder().Build())
            .BuildServiceProvider();
    }

    private static async Task<(int ExitCode, string Output)> RunCapturingConsoleAsync(Func<Task<int>> action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
