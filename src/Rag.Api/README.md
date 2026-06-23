# Rag.Api

ASP.NET Core Web API for the RAG platform.

## Purpose

Expose ingestion, ingestion job status, chunk preview, query, and health endpoints over the core platform services.

## Security Posture

Authentication is **opt-in**. Set `RAG_API_KEY` (or the `Api:ApiKey` configuration section) and every
request other than `GET /health`, `GET /` (the Swagger redirect), and the Swagger routes
(`/swagger/*`) must send a matching `X-API-Key` header, or the API responds `401` with a ProblemDetails
body that never echoes the expected key. The comparison is constant-time
(`CryptographicOperations.FixedTimeEquals`) to avoid a timing side channel. When `RAG_API_KEY` is not
set, the API accepts unauthenticated requests and logs a single startup warning; this keeps local
development, the samples, and the existing README flow working without extra setup. Deployments that
accept traffic from untrusted callers should set `RAG_API_KEY` or run behind an authenticating gateway,
bound to localhost otherwise. Two more consequences are worth calling out explicitly:

- `POST /documents` and `POST /chunk/preview` accept caller-supplied paths. Local reads are confined
  to `LocalSource:AllowedRoots`, which the API seeds with its own working directory. Set
  `LOCAL_SOURCE_ALLOWED_ROOTS` (or the `LocalSource:AllowedRoots` section) to ingest from elsewhere.
  Rejected paths return `403` and the response never echoes the rejected path.
- Any configured S3 or Azure Blob credentials are usable by every caller, who can enumerate and
  ingest whatever those credentials can reach. Scope them to the buckets and containers you intend
  to index.

Failures are returned as RFC 7807 problem responses. Internal errors are logged with detail but
answered with a generic message so store connection strings and host layout do not leak.

## Swagger UI

Swagger UI is enabled by default at `/swagger` when the API runs. The root path `/` redirects to the Swagger UI, and the OpenAPI JSON document is available at `/swagger/v1/swagger.json`.

## Endpoints

- `POST /documents`
- `GET /jobs/{id}`
- `POST /jobs/{id}/pause`
- `POST /jobs/{id}/cancel`
- `POST /jobs/{id}/resume`
- `POST /chunk/preview`
- `POST /query`
- `GET /health`

### `POST /documents`

Enqueues an ingestion job and returns `202 Accepted` immediately.

Inputs:

```json
{
  "sources": ["./samples", "s3://bucket/prefix", "azureblob://container/prefix"],
  "strategy": "recursive"
}
```

The legacy single-path shape is still accepted:

```json
{
  "path": "./samples",
  "strategy": "fixed"
}
```

Output:

```json
{
  "jobId": "generated-job-id",
  "status": "Queued"
}
```

The job is queued through the core `IIngestionJobQueue`; the default job store is in-process memory.
`Program.cs` calls `AddRagIngestionWorker()` right after `AddRagPlatform(...)` to register the
background `IngestionBackgroundService` that dequeues and runs these jobs. That registration is opt-in
and API-only: a host with no `IHost` (such as the CLI) must not call it, because a `BackgroundService`
never runs without one.

### `GET /jobs/{id}`

Returns the in-process ingestion job status.

Output fields include `jobId`, `status`, `sources`, `strategy`, `documentCount`, `chunkCount`, source progress, `error`, and timestamps. Per-document and per-chunk ID arrays are intentionally omitted so status responses stay small for large ingestion jobs.

### Job Control

`POST /jobs/{id}/pause` requests a cooperative pause. A running job stops after the current source item finishes and remains `Paused` until resumed.

`POST /jobs/{id}/resume` re-queues a paused job with the same `jobId`.

`POST /jobs/{id}/cancel` requests a cooperative cancel. A canceled job is terminal and will not be recovered on API restart.

### `POST /query`

Accepts the current core query fields plus the planned filter envelope:

```json
{
  "question": "What is the refund policy?",
  "topK": 5,
  "filter": {
    "origins": ["s3"],
    "documentIds": ["document-id"],
    "fileTypes": [".pdf"]
  }
}
```

Use `sources` for exact source URIs or paths and `origins` for `file`, `s3`, or `azureblob`. The API maps the filter envelope into the core `VectorSearchFilter` contract.

## Dependencies

Depends on `Rag.Core` and ASP.NET Core. `Program.cs` also references and calls all four opt-in
provider packages (`Rag.Providers.Aws`, `Rag.Providers.AzureBlob`, `Rag.Providers.Cosmos`,
`Rag.Providers.Mongo`) right after `AddRagPlatform(...)`, so every documented `DOC_STORE`, `JOB_STORE`,
and source scheme keeps working without extra setup. Provider behavior itself is selected through
configuration and DI.

## Configuration

The API reads configuration from `appsettings.json`, optional environment-specific JSON such as `appsettings.Development.json`, optional `.env`, and environment variables.

Precedence is:

1. The built-in `LocalSource:AllowedRoots` default (the process working directory).
2. JSON files.
3. `.env` values found in the working directory or a parent directory.
4. Environment variables.
5. Command-line arguments.

JSON uses the same option sections registered by `AddRagPlatform`, for example `Llm:Provider`, `DocumentStore:Provider`, and `VectorStore:Provider`. Environment variables can use the flat aliases from `.env.example`, such as `LLM_PROVIDER`, `LLM_EMBEDDING_ENDPOINT`, `LLM_CHAT_ENDPOINT`, `DOC_STORE`, and `VECTOR_STORE`.
