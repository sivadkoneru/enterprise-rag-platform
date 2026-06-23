# Source Modules

Contains the .NET projects for the enterprise RAG platform.

- `Rag.Core`: shared abstractions, models, parsers, chunking strategies, pipelines, the in-memory and
  file document stores, the Elasticsearch and in-memory vector stores, and dependency injection. Has
  no dependency on any backed provider SDK (AWS, Azure, Cosmos, or MongoDB), so a consumer that only
  wants the local file / in-memory path does not download or load one.
- `Rag.Providers.Aws`: opt-in AWS S3 document source. Owns the `AWSSDK.S3` dependency. Reference this
  and call `AddRagAwsS3(configuration)` to enable `s3://` sources.
- `Rag.Providers.AzureBlob`: opt-in Azure Blob Storage document source. Owns `Azure.Storage.Blobs`.
  Reference this and call `AddRagAzureBlob(configuration)` to enable `azureblob://` sources.
- `Rag.Providers.Cosmos`: opt-in Cosmos DB document store. Owns `Microsoft.Azure.Cosmos`. Reference
  this and call `AddRagCosmos(configuration)` to enable `DOC_STORE=cosmos`.
- `Rag.Providers.Mongo`: opt-in MongoDB document store and ingestion job store. Owns `MongoDB.Driver`.
  Reference this and call `AddRagMongo(configuration)` to enable `DOC_STORE=mongo` and
  `JOB_STORE=mongo`.
- `Rag.Api`: ASP.NET Core HTTP surface. References `Rag.Core` and all four provider packages so every
  documented provider keeps working out of the box.
- `Rag.Cli`: command-line surface. Same provider references as `Rag.Api`, for the same reason.

All projects should use explicit package versions and should keep provider-specific dependencies
isolated to their own provider package rather than `Rag.Core`. A consumer that wants a smaller
dependency graph can reference only the provider packages it needs (or none) and skip the matching
`AddRag*` call; requesting a `DOC_STORE`, `JOB_STORE`, or source scheme whose provider package was
never referenced throws a clear `InvalidOperationException` naming the missing package instead of
silently falling back to the in-memory default.
