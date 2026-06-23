# Rag.Core Dependency Injection

Service registration extensions for the platform.

`AddRagPlatform(configuration)` should compose parser adapters, the local source adapter, chunking strategies, LLM clients, the in-Core stores, vector search, ingestion job services, pipelines, options, and resilience policies. It has no dependency on any backed provider SDK.

Source providers are selected by URI scheme rather than a single provider selector: `file` is registered by `AddRagPlatform`, and `s3` / `azureblob` are registered by `Rag.Providers.Aws`'s `AddRagAwsS3` and `Rag.Providers.AzureBlob`'s `AddRagAzureBlob` respectively, when a host references those projects and calls them. All three end up in the same `IEnumerable<IDocumentSource>` collection that `DocumentSourceResolver` picks from by `CanRead`; an unreferenced cloud scheme is simply unsupported and throws `NotSupportedException`, same as before the split. `LLM_SYSTEM_PROMPT`, ingestion parallelism, vector filter behavior, and the opt-in `RAG_API_KEY` flow through options binding here; S3 endpoint/region settings and Azure Blob connection settings bind inside `AddRagAwsS3` / `AddRagAzureBlob` instead, since `Rag.Core` cannot reference `S3Options` or `AzureBlobOptions`.

## Provider Selection For Stores

`DOC_STORE` and `JOB_STORE` name a store the same way `s3`/`azureblob` name a source, but stores are
resolved by a single active implementation rather than picked from a collection, so they use **keyed
services** (`IServiceCollection.AddKeyedSingleton` / `IServiceProvider.GetKeyedService`, .NET 8+)
instead. `Rag.Providers.Mongo`'s `AddRagMongo` registers `MongoDocumentStore` as a keyed
`IDocumentStore` (`"mongo"`) and `MongoIngestionJobStore` as a keyed `IIngestionJobStore` (`"mongo"`);
`Rag.Providers.Cosmos`'s `AddRagCosmos` registers `CosmosDocumentStore` as a keyed `IDocumentStore`
(`"cosmos"`). `AddRagPlatform`'s internal resolvers (`ResolveDocumentStore`, `ResolveJobStore`,
`ResolveVectorStore`) handle the values Rag.Core implements itself (`memory`, `file`, and — because it
talks raw HTTP with no SDK dependency — `elasticsearch`) directly, and fall through to a keyed lookup
for everything else. An unset or blank provider value still defaults to `memory`, matching every
options class's own default, but an explicitly requested value that resolves to neither a Core-native
implementation nor a registered keyed service throws an `InvalidOperationException` naming the missing
provider package (for example, `DOC_STORE=mongo` without a reference to `Rag.Providers.Mongo` and a
call to `AddRagMongo(configuration)`) instead of silently falling back to `memory`.

`AddRagPlatform` does **not** register the background ingestion worker. `AddRagIngestionWorker(services)` is a separate, opt-in extension in the same file that adds `IngestionBackgroundService` as a hosted service. Only a host that runs under `IHost` (the API) can run a `BackgroundService`, so only the API calls it, right after `AddRagPlatform`. The CLI builds a plain `ServiceCollection`/`ServiceProvider` with no host and must not call it; it ingests inline instead.
