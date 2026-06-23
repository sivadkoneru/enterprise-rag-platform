# Rag.Core Stores

Document store repository contracts and adapters.

Store adapters are selected by `DOC_STORE` and do not change pipeline logic.

## Adapters

- `memory`: in-process dictionaries; the zero-configuration default.
- `file`: JSON documents and chunks under `DocumentStore:LocalPath`.
- `mongo`: MongoDB collections through `MongoDB.Driver`. Collection names come from `MONGO_DOCUMENTS_COLLECTION` and `MONGO_CHUNKS_COLLECTION`.
- `cosmos`: Cosmos DB containers through `Microsoft.Azure.Cosmos`, created on first use and partitioned by `/documentId`. Container names come from `COSMOS_DOCUMENTS_CONTAINER` and `COSMOS_CHUNKS_CONTAINER`.
