using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Cli;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Providers.Aws;
using Rag.Providers.AzureBlob;
using Rag.Providers.Cosmos;
using Rag.Providers.Mongo;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(EnvFile.LoadFromWorkingDirectory())
    .AddEnvironmentVariables()
    .Build();

// Opt-in provider packages: registered here so every documented DOC_STORE / JOB_STORE / source
// scheme (mongo, cosmos, s3, azureblob) keeps working exactly as it does today. A deployment that
// wants a smaller dependency graph can drop the reference and the matching AddRag* call instead.
using var services = new ServiceCollection()
    .AddRagPlatform(configuration)
    .AddRagAwsS3(configuration)
    .AddRagAzureBlob(configuration)
    .AddRagCosmos(configuration)
    .AddRagMongo(configuration)
    .BuildServiceProvider();

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Let the run unwind cooperatively through the CancellationToken instead of letting the
    // runtime terminate the process immediately.
    e.Cancel = true;
    cancellationSource.Cancel();
};

return await CliApplication.RunAsync(args, services, configuration, cancellationSource.Token).ConfigureAwait(false);
