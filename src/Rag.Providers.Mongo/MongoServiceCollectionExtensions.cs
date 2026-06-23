using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.Jobs;
using Rag.Core.Stores;

namespace Rag.Providers.Mongo;

/// <summary>
/// Opt-in registration for MongoDB as a document store and ingestion job store. Kept out of
/// Rag.Core so a consumer that never selects <c>DOC_STORE=mongo</c> or <c>JOB_STORE=mongo</c> does
/// not pull in MongoDB.Driver and its transitive dependencies. <see cref="Rag.Core.Configuration.DocumentStoreOptions"/>
/// and <see cref="Rag.Core.Configuration.JobStoreOptions"/> binding already happens in
/// <c>AddRagPlatform</c>, so this extension only needs to register the keyed stores themselves.
/// </summary>
public static class MongoServiceCollectionExtensions
{
    public static IServiceCollection AddRagMongo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedSingleton<IDocumentStore, MongoDocumentStore>("mongo");
        services.AddKeyedSingleton<IIngestionJobStore, MongoIngestionJobStore>("mongo");

        return services;
    }
}
