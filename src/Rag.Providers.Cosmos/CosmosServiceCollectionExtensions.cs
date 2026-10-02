using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.Stores;
using Rag.Core.Workbench;
using Rag.Providers.Cosmos.Stores;

namespace Rag.Providers.Cosmos;

/// <summary>
/// Opt-in registration for Cosmos DB as a document store. Kept out of Rag.Core so a consumer that
/// never selects <c>DOC_STORE=cosmos</c> does not pull in Microsoft.Azure.Cosmos and its transitive
/// dependencies. <see cref="Rag.Core.Configuration.DocumentStoreOptions"/> binding (connection
/// string, endpoint/key, database, and container names) already happens in
/// <c>AddRagPlatform</c>, so this extension only needs to register the keyed store itself.
/// </summary>
public static class CosmosServiceCollectionExtensions
{
    public static IServiceCollection AddRagCosmos(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedSingleton<IDocumentStore, CosmosDocumentStore>("cosmos");
        services.AddKeyedSingleton<IWorkbenchStateStore, CosmosWorkbenchStateStore>("cosmos");

        return services;
    }
}
