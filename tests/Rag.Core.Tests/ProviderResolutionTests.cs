using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Providers.Mongo;
using Xunit;

namespace Rag.Core.Tests;

/// <summary>
/// An explicitly requested provider whose package was never wired up must fail loudly and name the
/// missing package, not silently fall back to the in-memory default. Rag.Core.Tests intentionally
/// does not reference Rag.Providers.Cosmos, so "cosmos" doubles as a stand-in for any provider whose
/// assembly was never referenced at all; "mongo" (referenced, but its AddRagMongo call omitted)
/// shows that referencing the assembly alone is not enough.
/// </summary>
public sealed class ProviderResolutionTests
{
    [Fact]
    public void RequestingCosmosDocumentStoreWithoutTheProviderPackageThrows()
    {
        var services = new ServiceCollection()
            .AddRagPlatform(Configuration(("DOC_STORE", "cosmos")))
            .BuildServiceProvider();

        var act = () => services.GetRequiredService<IDocumentStore>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Rag.Providers.Cosmos*AddRagCosmos*");
    }

    [Fact]
    public void RequestingMongoDocumentStoreWithoutCallingAddRagMongoThrows()
    {
        // Rag.Providers.Mongo is referenced (for AddRagPlatformSelectsMongoJobStoreFromConfiguration
        // elsewhere in this project), but AddRagMongo is deliberately not called here: referencing
        // the assembly must not be enough on its own to make "mongo" available.
        var services = new ServiceCollection()
            .AddRagPlatform(Configuration(("DOC_STORE", "mongo")))
            .BuildServiceProvider();

        var act = () => services.GetRequiredService<IDocumentStore>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Rag.Providers.Mongo*AddRagMongo*");
    }

    [Fact]
    public void RequestingAnUnrecognizedDocumentStoreProviderThrowsWithSupportedValues()
    {
        var services = new ServiceCollection()
            .AddRagPlatform(Configuration(("DOC_STORE", "does-not-exist")))
            .BuildServiceProvider();

        var act = () => services.GetRequiredService<IDocumentStore>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does-not-exist*")
            .WithMessage("*memory*");
    }

    [Fact]
    public void RequestingMongoJobStoreWithoutCallingAddRagMongoThrows()
    {
        var services = new ServiceCollection()
            .AddRagPlatform(Configuration(("JOB_STORE", "mongo")))
            .BuildServiceProvider();

        var act = () => services.GetRequiredService<IIngestionJobStore>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Rag.Providers.Mongo*AddRagMongo*");
    }

    [Fact]
    public void AnUnrecognizedVectorStoreProviderThrowsInsteadOfSilentlyFallingBackToMemory()
    {
        // Regression guard: today's resolver used to swallow any unrecognized VECTOR_STORE value
        // and silently return the in-memory store. An explicitly requested value that is not
        // "memory" or "elasticsearch" must fail loudly instead.
        var services = new ServiceCollection()
            .AddRagPlatform(Configuration(("VECTOR_STORE", "azuresearch")))
            .BuildServiceProvider();

        var act = () => services.GetRequiredService<IVectorStore>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*azuresearch*")
            .WithMessage("*elasticsearch*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsetOrBlankVectorStoreStillDefaultsToMemory(string? provider)
    {
        var services = new ServiceCollection()
            .AddRagPlatform(Configuration(("VECTOR_STORE", provider)))
            .BuildServiceProvider();

        var act = () => services.GetRequiredService<IVectorStore>();

        act.Should().NotThrow("an unset or blank VECTOR_STORE must keep defaulting to the in-memory store");
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => setting.Value))
            .Build();
    }
}
