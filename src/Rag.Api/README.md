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

Returns ingestion job status from the configured `JOB_STORE`. The default memory store keeps jobs for the current process; `JOB_STORE=mongo` persists them across API restarts.

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

## Live workbench API (`/api/v1`)

The versioned workflow is additive; the routes above and the published evaluation harness remain
unchanged. Call `AddRagWorkbench(configuration)` in a hosted application after registering the
platform and selected providers. These routes use the same `X-API-Key` policy and source allowlists.

| Route | Contract |
| --- | --- |
| `GET /capabilities` | Selected providers, effective embedding model/dimensions, supported strategies, and active `defaultQuery` controls. |
| `GET /configuration` | Shared configuration catalog with effective values; secrets have `value: null` and a `configured` flag. |
| `GET /readiness` | Store connectivity and model configuration status. It does not invoke embedding, chat, or reranker inference. |
| `POST /checks/embedding`, `/checks/chat`, `/checks/reranker` | Explicit provider requests; return `name`, `status`, `message`, `latencyMs`, and safe `details`. |
| `GET`, `POST /corpora` | Array of corpora; creation accepts `{ "name": "Policies", "description": "Client documents" }`. |
| `GET`, `POST /corpora/{id}/profiles` | Immutable indexing profiles; creation accepts `name`, `strategy`, `chunkSize`, `chunkOverlap`, `embeddingModel`, and `embeddingDimensions`. |
| `POST /profiles/{id}/ingestions` | Accepts `{ "sources": ["/srv/rag/documents"] }`; returns a background job with HTTP 202. |
| `GET /jobs`, `/jobs/{id}` | Persisted job status; the list accepts optional `profileId`. |
| `POST /jobs/{id}/pause`, `/resume`, `/cancel` | Control indexing and evaluation jobs. |
| `GET /profiles/{id}/documents`, `/chunks` | Pages `{ items, total, offset, limit }`; chunks accept optional `documentId`. |
| `GET /profiles/{id}/documents/{documentId}` | Parsed document text and its chunk count. |
| `POST /queries` | Detailed query stream with SSE `stage`, `result`, and sanitized `error` events. |
| `POST /evaluations` | Validated own-corpus evaluation dataset; returns a background job with HTTP 202. |
| `GET /evaluations/{id}`, `/evaluations/{id}/export` | Immutable result snapshots; export downloads JSON. |

Detailed queries accept `question`, `corpusId`, `profileId`, `topK`, `mode` (`vector` or `hybrid`),
`reranker`, `minRelevance`, `neighbors`, and `maxContextTokens`. Omitted controls use the bound `Query`
options. A live profile gets its own Elasticsearch index named `{configured-index}-{profile-id}`;
its document/chunk IDs also include profile identity. Reusing a source with different chunk settings
or a newly configured embedding model/dimension requires a new profile. Existing unversioned queries
continue using the original configured index.

Vector scores are cosine normalized to `[0,1]`. Hybrid retrieval uses actual Elasticsearch BM25
text search plus reciprocal rank fusion (`k=60`); in-memory retrieval computes BM25 over the isolated
profile text. HTTP reranking reorders the selected candidate pool. The vector threshold, complete
chunk budget, and same-document neighbors control context admission. Neighbor scores are `null`
because adjacency does not measure similarity. Stage durations are measured; total latency includes
store reads and stream writes as well as stage work.

Model-emitted numeric citations are normalized to real chunk IDs. Every emitted distinct reference
is checked against admitted context; invalid references remain in `citations` with `valid: false`
and in `invalidCitations`. This validates reference integrity, not entailment. No context produces
an explicit abstention without a chat request. The live deterministic client extracts a substantive
sentence from the real context with a real citation; it does not change the deterministic benchmark
client. Context tokens use `ceil(chars/4)`. Output tokens use provider usage when supplied; otherwise
they are estimated, with `tokenUsageKind` identifying the source. Provider prompt/total counts are
returned separately when available.

### HTTP reranker

Configure `Reranker:Endpoint`, `ApiKey`, `Model`, and `TimeoutSeconds`, or their `RERANKER_*` aliases.
The endpoint receives `POST { "model": "configured-model", "query": "question", "documents":
["passage text"], "top_n": 1 }`, with bearer authorization when a key is configured. It must return
`{ "results": [{ "index": 0, "relevance_score": 0.91 }] }` with exactly one finite score per candidate.
Missing, duplicate, or out-of-range indices fail the stage; no simulated reranking replaces a failure.

### Own-data evaluation input

```json
{
  "profileIds": ["profile-id"],
  "topK": 5,
  "mode": "vector",
  "minRelevance": 0.7,
  "questions": [
    {
      "id": "refund-window",
      "question": "What is the refund window?",
      "expectedSourceFile": "policies.md",
      "goldAnchors": [{ "phrase": "Refunds are available within thirty days." }],
      "expectedAnswer": "Refunds are available within thirty days.",
      "expectedAbstention": false
    },
    {
      "id": "not-covered",
      "question": "What is the orbital period of Jupiter?",
      "goldAnchors": [],
      "expectedAbstention": true
    }
  ]
}
```

All compared profiles must belong to one corpus. Answerable cases require anchors resolving to a
unique indexed source before any model call; unanswerable cases require no anchors and
`expectedAbstention: true`. Results contain actual answers, retrieval/context passages, citations,
stage timings, question inputs, retrieval settings, profile settings, and source-content revision
fingerprints. Recall/MRR use recovered gold anchors; citation metrics score actual emitted references,
including invalid references in precision. Abstention accuracy scores actual answer behavior.
Groundedness is explicitly a **lexical token-overlap proxy**, not a semantic judge. Optional expected
answers use normalized exact matching; absent reference metrics are `null`.

Catalog/profile/document/result state follows `DOC_STORE`; live job state follows `JOB_STORE`.
Mongo and Cosmos adapters stay in provider projects. Memory storage is temporary; file catalog state
lives under `DocumentStore:LocalPath/workbench`. Paused/canceled jobs retain their state, and queued or
interrupted jobs recover after restart. Store availability failures during startup recovery retry
without shutting down the API.
