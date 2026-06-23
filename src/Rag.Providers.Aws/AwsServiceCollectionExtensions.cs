using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Configuration;
using Rag.Core.Sources;

namespace Rag.Providers.Aws;

/// <summary>
/// Opt-in registration for AWS S3 as a document source. Kept out of Rag.Core so a consumer that
/// never ingests from S3 does not pull in AWSSDK.S3 and its transitive dependencies.
/// </summary>
public static class AwsServiceCollectionExtensions
{
    public static IServiceCollection AddRagAwsS3(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<S3Options>(options =>
        {
            configuration.GetSection("S3").Bind(options);
            options.Region = configuration["S3_REGION"] ?? configuration["AWS_REGION"] ?? configuration["AWS_DEFAULT_REGION"] ?? options.Region;
            options.ServiceUrl = configuration["S3_ENDPOINT"] ?? configuration["S3_SERVICE_URL"] ?? configuration["AWS_ENDPOINT_URL"] ?? options.ServiceUrl;
            options.ForcePathStyle = Bool(configuration["S3_FORCE_PATH_STYLE"], options.ForcePathStyle);
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var s3Options = sp.GetRequiredService<IOptions<S3Options>>().Value;
            var config = new AmazonS3Config
            {
                ForcePathStyle = s3Options.ForcePathStyle
            };
            if (!string.IsNullOrWhiteSpace(s3Options.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region);
            }

            if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
            {
                config.ServiceURL = s3Options.ServiceUrl;
            }

            return new AmazonS3Client(config);
        });

        services.AddSingleton<IDocumentSource, AwsS3DocumentSource>();

        return services;
    }

    private static bool Bool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
