# Rag.Core Stores

Document store repository contracts and adapters.

Store adapters are selected by `DOC_STORE` and do not change pipeline logic.

## Adapters

- `memory`: in-process dictionaries; the zero-configuration default. Lives in this project.
- `file`: JSON documents and chunks under `DocumentStore:LocalPath`. Lives in this project.
- `mongo`: MongoDB collections through `MongoDB.Driver`. Collection names come from `MONGO_DOCUMENTS_COLLECTION` and `MONGO_CHUNKS_COLLECTION`. Lives in `Rag.Providers.Mongo`; reference that project and call `AddRagMongo(configuration)` to enable it.
- `cosmos`: Cosmos DB containers through `Microsoft.Azure.Cosmos`, created on first use and partitioned by `/documentId`. Container names come from `COSMOS_DOCUMENTS_CONTAINER` and `COSMOS_CHUNKS_CONTAINER`. Lives in `Rag.Providers.Cosmos`; reference that project and call `AddRagCosmos(configuration)` to enable it.

## Provider Selection

Only `memory` and `file` are known to `Rag.Core`, which cannot name the Mongo or Cosmos types since
they live in separate assemblies. `AddRagMongo` / `AddRagCosmos` each register their store as a keyed
`IDocumentStore` singleton (keyed `"mongo"` / `"cosmos"`); `AddRagPlatform`'s resolver looks up the
configured `DOC_STORE` value by that key. Requesting `mongo` or `cosmos` without referencing the
matching provider project and calling its `AddRag*` method throws an `InvalidOperationException` that
names the missing package, rather than silently falling back to `memory`.
