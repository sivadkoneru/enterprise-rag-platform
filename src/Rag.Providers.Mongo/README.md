# Rag.Providers.Mongo

Opt-in MongoDB document store and ingestion job store for the enterprise RAG platform.

## Purpose

Enable `DOC_STORE=mongo` and `JOB_STORE=mongo` without forcing every consumer of `Rag.Core` to
download and load `MongoDB.Driver` (and its transitive `SharpCompress` / `Snappier` dependencies).
Both stores live here instead of in `Rag.Core`.

## Usage

Reference this project and call `AddRagMongo(configuration)` after `AddRagPlatform(configuration)`:

```csharp
services
    .AddRagPlatform(configuration)
    .AddRagMongo(configuration);
```

`DocumentStoreOptions` and `JobStoreOptions` (connection strings, database/collection names —
`MONGO_CONNECTION_STRING`, `MONGO_DATABASE`, `MONGO_CHUNKS_COLLECTION`, `MONGO_DOCUMENTS_COLLECTION`,
`MONGO_JOBS_COLLECTION`) already bind inside `AddRagPlatform`, since `Rag.Core` owns those shared
options types. `AddRagMongo` only needs to register:

- `MongoDocumentStore` as a keyed `IDocumentStore` singleton (key `"mongo"`), looked up when `DOC_STORE=mongo`.
- `MongoIngestionJobStore` as a keyed `IIngestionJobStore` singleton (key `"mongo"`), looked up when `JOB_STORE=mongo`.

Both types keep their original `Rag.Core.Stores` / `Rag.Core.Jobs` namespaces (they moved projects,
not namespaces), so existing `using` directives that reference them stay valid. `MongoIngestionJobStore`
depends on `Rag.Core.Jobs.IngestionJobTransitions` for shared terminal-status and processed-count
backfill rules, which is `public` in `Rag.Core` for exactly this reason.

Requesting `DOC_STORE=mongo` or `JOB_STORE=mongo` without this reference and call throws an
`InvalidOperationException` that names this package and `AddRagMongo`, instead of silently falling
back to the in-memory store.

## Dependencies

`MongoDB.Driver`, and `Rag.Core` for the `IDocumentStore` / `IIngestionJobStore` contracts it implements.


## Live workbench persistence

`AddRagMongo` also registers keyed `IWorkbenchStateStore` and `IWorkbenchJobStateStore` adapters.
`AddRagWorkbench` selects them through `DOC_STORE=mongo` and `JOB_STORE=mongo`, respectively.
Corpus/profile/document/result records use the configured document database; live jobs use the
configured job database and connection. Records live in dedicated `workbench_*` collections and
store versioned JSON payloads, separate from the original ingestion-job schema. Recreating the
adapter retains catalog, job, and evaluation state. No Mongo SDK dependency is added to `Rag.Core`.
