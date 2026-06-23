# Rag.Providers.AzureBlob

Opt-in Azure Blob Storage document source for the enterprise RAG platform.

## Purpose

Enable `azureblob://container/prefix` document ingestion without forcing every consumer of
`Rag.Core` to download and load `Azure.Storage.Blobs`. Everything provider-specific — the SDK
dependency, `AzureBlobOptions` binding, and the `AzureBlobDocumentSource` adapter itself — lives here
instead of in `Rag.Core`.

## Usage

Reference this project and call `AddRagAzureBlob(configuration)` after `AddRagPlatform(configuration)`:

```csharp
services
    .AddRagPlatform(configuration)
    .AddRagAzureBlob(configuration);
```

This registers:

- `AzureBlobOptions` binding (`AzureBlob:*` configuration section, `AZURE_BLOB_CONNECTION_STRING`, `AZURE_BLOB_SERVICE_URI` / `AZURE_BLOB_ENDPOINT`).
- `AzureBlobDocumentSource` as another `IDocumentSource` in the collection `DocumentSourceResolver` picks from by `CanRead`. The client itself is built lazily per container from `AzureBlobOptions`, supporting either a connection string (Azurite included) or a service URI with ambient credentials.

`AzureBlobDocumentSource` keeps the `Rag.Core.Sources` namespace (it moved projects, not namespaces),
and `AzureBlobOptions` keeps the `Rag.Core.Configuration` namespace, so existing `using` directives
that reference either type stay valid.

Without this reference and call, `azureblob://` source URIs throw the same `NotSupportedException`
`DocumentSourceResolver` already throws for any unrecognized scheme — no other wiring changes.

## Dependencies

`Azure.Storage.Blobs`, and `Rag.Core` for the `IDocumentSource` / `CloudDocumentSource` contracts it implements against.
