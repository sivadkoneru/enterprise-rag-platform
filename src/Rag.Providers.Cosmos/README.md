# Rag.Providers.Cosmos

Opt-in Cosmos DB document store for the enterprise RAG platform.

## Purpose

Enable `DOC_STORE=cosmos` without forcing every consumer of `Rag.Core` to download and load
`Microsoft.Azure.Cosmos` (and its `Newtonsoft.Json` dependency, which `CosmosDocumentStore` uses
directly for the wire-format `JsonProperty` attributes). The store itself lives here instead of in
`Rag.Core`.

## Usage

Reference this project and call `AddRagCosmos(configuration)` after `AddRagPlatform(configuration)`:

```csharp
services
    .AddRagPlatform(configuration)
    .AddRagCosmos(configuration);
```

`DocumentStoreOptions` (connection string, endpoint/key, database, and container names —
`COSMOS_CONNECTION_STRING`, `COSMOS_ENDPOINT` / `COSMOS_KEY`, `COSMOS_DATABASE`,
`COSMOS_CHUNKS_CONTAINER`, `COSMOS_DOCUMENTS_CONTAINER`) already binds inside `AddRagPlatform`, since
`Rag.Core` owns that shared options type. `AddRagCosmos` only needs to register `CosmosDocumentStore`
as a keyed `IDocumentStore` singleton (key `"cosmos"`); `AddRagPlatform`'s resolver looks it up by
that key when `DOC_STORE=cosmos`.

`CosmosDocumentStore` keeps the `Rag.Core.Stores` namespace (it moved projects, not namespaces), so
existing `using` directives that reference it stay valid.

Requesting `DOC_STORE=cosmos` without this reference and call throws an `InvalidOperationException`
that names this package and `AddRagCosmos`, instead of silently falling back to the in-memory store.

## Dependencies

`Microsoft.Azure.Cosmos`, `Newtonsoft.Json`, and `Rag.Core` for the `IDocumentStore` contract it implements.
