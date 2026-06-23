# Rag.Core Sources

Document source adapters enumerate supported inputs before parser resolution.

## Purpose

Keep local file, AWS S3, and Azure Blob Storage enumeration behind provider-neutral contracts. Pipelines should ask the source resolver for source items and then hand materialized paths to the existing parser resolver.

## Where The Adapters Live

Only `LocalDirectorySource` (the `file` scheme) lives in this folder together with the shared
`CloudDocumentSource` base class, `DocumentSourceResolver`, and `DocumentSourceSupport`. The concrete
`s3` and `azureblob` adapters — `AwsS3DocumentSource` and `AzureBlobDocumentSource` — live in the
sibling `Rag.Providers.Aws` and `Rag.Providers.AzureBlob` projects so that `Rag.Core` has no
dependency on `AWSSDK.S3` or `Azure.Storage.Blobs`. Both keep the `Rag.Core.Sources` namespace, so
every existing `using` stays valid; this is a packaging change, not a source-breaking one. A host
that wants `s3://` or `azureblob://` support references the matching provider project and calls
`AddRagAwsS3(configuration)` / `AddRagAzureBlob(configuration)`, which register the adapter as another
`IDocumentSource` in the same collection `DocumentSourceResolver` already picks from by `CanRead`. An
unreferenced cloud scheme simply is not supported, and resolving it throws the existing
`NotSupportedException` — no provider-selector wiring is needed for sources, unlike stores.

## Usage

Source URIs use these schemes:

- `file:///absolute/path`, `./relative/path`, or `/absolute/path` for local files and directories.
- `s3://bucket/prefix` for AWS S3 or LocalStack.
- `azureblob://container/prefix` for Azure Blob Storage or Azurite.

Local directory enumeration is recursive and includes `.txt`, `.md`, `.pdf`, `.html`, `.htm`, `.json`, `.jsonl`, `.ndjson`, `.jsonl.gz`, `.ndjson.gz`, and `.csv` files in stable order. Schema sidecars such as `*.schema.json` and `rag-ingestion.schema.json` are discovered and passed as source item attributes, but are not ingested as documents. Cloud adapters list objects under the prefix, download supported file types and matching schema sidecars to temporary files, and clean them up after parsing.

## Allowed Roots

`LocalSource:AllowedRoots` (or the flat `LOCAL_SOURCE_ALLOWED_ROOTS` list, separated by `:` on
Linux/macOS and `;` on Windows) restricts which directories the `file` source may read. An empty
list means unrestricted, which is the default for library and CLI use where the caller already runs
with the invoking user's privileges. Hosts that accept source paths from untrusted callers must set
at least one root; the API seeds its working directory by default.

Paths are canonicalized before the check, and each enumerated file is validated at its final link
target, so neither `..` traversal nor a symlink inside an allowed root can escape. Violations throw
`SourcePathNotAllowedException`, which deliberately omits the rejected path.

## Allowed Prefixes

`CloudSource:AllowedPrefixes` (or the flat `CLOUD_SOURCE_ALLOWED_PREFIXES` list) is the cloud
equivalent of allowed roots: it restricts which `s3://` and `azureblob://` prefixes may be ingested.
The list is comma separated rather than path separated, because `:` appears in every cloud scheme.
An empty list means unrestricted, matching the local default. Matching is case-insensitive and
requires a segment boundary, so `s3://corp-docs` does not permit `s3://corp-docs-public`. Violations
throw the same `SourcePathNotAllowedException` the local source uses, so the API maps both to 403
without echoing the rejected URI.

## Shared Cloud Behavior

`CloudDocumentSource` holds everything the S3 and Azure Blob adapters have in common: scheme
matching, URI parsing, supported-type filtering, stable ordering, temp-file materialization, schema
sidecar resolution, cleanup, and the allowed-prefix check. A backend supplies only three things —
how to list keys under a prefix, how to download one key, and which backend-specific attributes to
attach. Adding a third cloud backend means implementing those three members, not another copy of
the enumeration loop.

## Inputs And Outputs

Inputs are source URIs and provider options. Outputs are source items with a materialized path, original source URI, origin (`file`, `s3`, or `azureblob`), file name, extension, and metadata needed for document and vector records.

## Dependencies

Local sources (this project) depend only on `System.IO`. S3 sources depend on `AWSSDK.S3` with
optional LocalStack endpoint configuration, and live in `Rag.Providers.Aws`. Azure Blob sources
depend on `Azure.Storage.Blobs` with optional Azurite connection-string configuration, and live in
`Rag.Providers.AzureBlob`.
