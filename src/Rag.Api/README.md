# Rag.Api

ASP.NET Core Web API for the RAG platform.

## Purpose

Expose ingestion, ingestion job status, chunk preview, query, and health endpoints over the core platform services.

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

Depends on `Rag.Core` and ASP.NET Core. Provider behavior is selected through configuration and DI.

## Configuration

The API reads configuration from `appsettings.json`, optional environment-specific JSON such as `appsettings.Development.json`, optional `.env`, and environment variables.

Precedence is:

1. JSON files.
2. `.env` values found in the working directory or a parent directory.
3. Environment variables.

JSON uses the same option sections registered by `AddRagPlatform`, for example `Llm:Provider`, `DocumentStore:Provider`, and `VectorStore:Provider`. Environment variables can use the flat aliases from `.env.example`, such as `LLM_PROVIDER`, `LLM_EMBEDDING_ENDPOINT`, `LLM_CHAT_ENDPOINT`, `DOC_STORE`, and `VECTOR_STORE`.
