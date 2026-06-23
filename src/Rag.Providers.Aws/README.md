# Rag.Providers.Aws

Opt-in AWS S3 document source for the enterprise RAG platform.

## Purpose

Enable `s3://bucket/prefix` document ingestion without forcing every consumer of `Rag.Core` to
download and load `AWSSDK.S3`. Everything provider-specific — the SDK dependency, the `IAmazonS3`
registration, `S3Options` binding, and the `AwsS3DocumentSource` adapter itself — lives here instead
of in `Rag.Core`.

## Usage

Reference this project and call `AddRagAwsS3(configuration)` after `AddRagPlatform(configuration)`:

```csharp
services
    .AddRagPlatform(configuration)
    .AddRagAwsS3(configuration);
```

This registers:

- `S3Options` binding (`S3:*` configuration section, `S3_REGION` / `AWS_REGION` / `AWS_DEFAULT_REGION`, `S3_ENDPOINT` / `S3_SERVICE_URL` / `AWS_ENDPOINT_URL`, `S3_FORCE_PATH_STYLE`).
- An `IAmazonS3` singleton built from those options (supports a LocalStack-style endpoint override).
- `AwsS3DocumentSource` as another `IDocumentSource` in the collection `DocumentSourceResolver` picks from by `CanRead`.

`AwsS3DocumentSource` keeps the `Rag.Core.Sources` namespace (it moved projects, not namespaces), and
`S3Options` keeps the `Rag.Core.Configuration` namespace, so existing `using` directives that reference
either type stay valid.

Without this reference and call, `s3://` source URIs throw the same `NotSupportedException`
`DocumentSourceResolver` already throws for any unrecognized scheme — no other wiring changes.

## Dependencies

`AWSSDK.S3`, and `Rag.Core` for the `IDocumentSource` / `CloudDocumentSource` contracts it implements against.
