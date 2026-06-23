# Rag.Core Sources

Document source adapters enumerate supported inputs before parser resolution.

## Purpose

Keep local file, AWS S3, and Azure Blob Storage enumeration behind provider-neutral contracts. Pipelines should ask the source resolver for source items and then hand materialized paths to the existing parser resolver.

## Usage

Source URIs use these schemes:

- `file:///absolute/path`, `./relative/path`, or `/absolute/path` for local files and directories.
- `s3://bucket/prefix` for AWS S3 or LocalStack.
- `azureblob://container/prefix` for Azure Blob Storage or Azurite.

Local directory enumeration is recursive and includes `.txt`, `.md`, and `.pdf` files in stable order. Cloud adapters should list objects under the prefix, download supported file types to temporary files, and clean them up after parsing.

## Inputs And Outputs

Inputs are source URIs and provider options. Outputs are source items with a materialized path, original source URI, origin (`file`, `s3`, or `azureblob`), file name, extension, length when known, and metadata needed for document and vector records.

## Dependencies

Local sources depend on `System.IO`. S3 sources depend on `AWSSDK.S3` with optional LocalStack endpoint configuration. Azure Blob sources depend on `Azure.Storage.Blobs` with optional Azurite connection-string configuration.
