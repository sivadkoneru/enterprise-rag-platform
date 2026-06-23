using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Sources;

namespace Rag.Providers.AzureBlob;

/// <summary>
/// Opt-in registration for Azure Blob Storage as a document source. Kept out of Rag.Core so a
/// consumer that never ingests from Azure Blob does not pull in Azure.Storage.Blobs and its
/// transitive dependencies.
/// </summary>
public static class AzureBlobServiceCollectionExtensions
{
    public static IServiceCollection AddRagAzureBlob(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureBlobOptions>(options =>
        {
            configuration.GetSection("AzureBlob").Bind(options);
            options.ConnectionString = configuration["AZURE_BLOB_CONNECTION_STRING"] ?? options.ConnectionString;
            options.ServiceUri = configuration["AZURE_BLOB_SERVICE_URI"] ?? configuration["AZURE_BLOB_ENDPOINT"] ?? options.ServiceUri;
        });

        services.AddSingleton<IDocumentSource, AzureBlobDocumentSource>();

        return services;
    }
}
