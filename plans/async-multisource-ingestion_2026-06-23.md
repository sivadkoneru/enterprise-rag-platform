# Plan — Async Multi-Source Ingestion + Grounded Retrieval

> Generated 2026-06-23 via `/plan-for-goal`. Executes as subagent-driven steps with one git commit per step. Extends the existing platform plan: [enterprise-rag-platform_2026-06-23.md](enterprise-rag-platform_2026-06-23.md).

## Brief

Add asynchronous, background-job ingestion that chunks documents from local directories, AWS S3 prefixes, and Azure Blob Storage prefixes into the existing chunk + vector stores, so a single store can hold a mix of datasources and file types; queries then retrieve only the appropriate documents (optionally scoped by source/document/file type) and answer through a configurable, always-present grounding system prompt that restricts answers to the supplied context.

## Stack

- .NET 10 (LTS), C# — fallback .NET 8 if 10 unavailable (matches current `Directory.Build.props`)
- Existing: `Rag.Core` (DI via `AddRagPlatform`), `Rag.Api` (ASP.NET Core minimal API), `Rag.Cli` (System.CommandLine)
- AWSSDK.S3 (S3 source adapter) — pin version in `Directory.Packages.props`
- Azure.Storage.Blobs (Azure Blob source adapter) — pin version
- `Microsoft.Extensions.Hosting` `BackgroundService` + `System.Threading.Channels` (background job queue)
- Existing: Elastic.Clients.Elasticsearch 8.15.x (kNN + metadata filter), MongoDB.Driver, Microsoft.Azure.Cosmos
- Tests: xUnit + FluentAssertions + Testcontainers; LocalStack (S3) + Azurite (Blob) for integration
- Docker Compose (add LocalStack + Azurite alongside Elasticsearch + MongoDB)

## Current State (verified in repo)

- Ingestion is local-filesystem only and sequential: [IngestionPipeline.cs:54-70](../src/Rag.Core/Pipelines/IngestionPipeline.cs#L54-L70) enumerates files from one `Path` and processes them in a blocking `foreach`.
- No source connector abstraction; parsers are path-based: [IDocumentParser.cs](../src/Rag.Core/Abstractions/IDocumentParser.cs).
- A system prompt **already exists** but is hardcoded and weak, and the deterministic client ignores it: [QueryPipeline.cs:23](../src/Rag.Core/Pipelines/QueryPipeline.cs#L23), [DeterministicLlmClient.cs:28-36](../src/Rag.Core/Llm/DeterministicLlmClient.cs#L28-L36). There is no `LLM_SYSTEM_PROMPT` in `.env.example`.
- Retrieval has no metadata filter: [IVectorStore.cs:11](../src/Rag.Core/Abstractions/IVectorStore.cs#L11). Metadata (`source`, `fileName`, `chunkIndex`) is already written to `VectorRecord` at [IngestionPipeline.cs:31-40](../src/Rag.Core/Pipelines/IngestionPipeline.cs#L31-L40) but never queried.
- `IngestionRequest(string Path, string? Strategy)` and `QueryRequest(string Question, int TopK)` are the request contracts: [Requests.cs](../src/Rag.Core/Models/Requests.cs).

## Scope — Functionality

1. **Source abstraction.** Introduce `IDocumentSource` + `SourceItem` + `IDocumentSourceResolver`; resolve by URI scheme (`file` / `s3` / `azureblob`). Cloud objects materialize to a temp working directory so the existing path-based `IDocumentParser` contract stays unchanged; temp files are cleaned up after parse.
2. **Local directory source.** Refactor existing enumeration behind `LocalDirectorySource` with identical behavior (recursive, `.txt`/`.md`/`.pdf` filter, stable ordering).
3. **AWS S3 source.** `AwsS3DocumentSource` lists objects under `s3://bucket/prefix`, downloads supported types, carries object key + bucket as metadata. Bound by `S3Options` (region, endpoint override for LocalStack, credentials via standard AWS chain).
4. **Azure Blob source.** `AzureBlobDocumentSource` enumerates `azureblob://container/prefix`, downloads supported blobs, carries container + blob name as metadata. Bound by `AzureBlobOptions` (connection string / endpoint for Azurite).
5. **Background ingestion jobs.** `POST /documents` enqueues an `IngestionJob` and returns `202` + `jobId` immediately. An `IngestionBackgroundService` (`BackgroundService`) drains a `Channel`-backed queue and runs the pipeline with bounded parallelism across source items. `GET /jobs/{id}` reports status (`Queued`/`Running`/`Succeeded`/`Failed`), counts, document ids, and error. Job state lives behind `IIngestionJobStore` (in-memory default, pluggable).
6. **Mixed-store ingestion.** One ingestion request may include multiple source URIs; each item records its `Origin` (`file`/`s3`/`azureblob`) and `Extension`/`fileType` in `DocumentMetadata` and `VectorRecord.Metadata`, so a single store mixes datasources and file types.
7. **Scoped retrieval.** Extend `IVectorStore.SearchAsync` with an optional `VectorSearchFilter` (documentIds, sources, origins, fileTypes). Implement filtering in `InMemoryVectorStore` (in code) and `ElasticsearchVectorStore` (kNN `filter`). `QueryRequest` gains optional filter fields so a question can target selected documents.
8. **Configurable grounded system prompt.** Add `LlmOptions.SystemPrompt` with a strong default ("answer only from the supplied context; if the answer is not present, say you don't know; always cite sources"). `QueryPipeline` always sends it from options instead of the hardcoded string; `DeterministicLlmClient` honors the grounding contract (returns a no-answer response when context is empty). Add `LLM_SYSTEM_PROMPT` to `.env.example`.
9. **Surfaces aligned.** API: `POST /documents` (202 + jobId), `GET /jobs/{id}`, `POST /query` with filters. CLI: `ingest <uri...> [--wait]` (prints jobId; `--wait` polls to completion), `jobs status <id>`, and `query` filter flags (`--source`, `--document`, `--type`). The CLI runs the job inline (short-lived process) but reports the same jobId contract for parity with the API.

## Out of Scope

- Document formats beyond `.txt`/`.md`/`.pdf` (docx/html/OCR) — the source adapters fetch any object but ingestion still filters to the three supported parsers.
- Persistent/distributed job store or queue (Redis, SQS, Service Bus) — in-memory `IIngestionJobStore` + in-process `Channel` only; the interface leaves the swap point.
- Incremental/delta sync, change-feed watching, or scheduled re-crawls of S3/Blob — ingestion is on-demand per request.
- Authn/authz on the API, multi-tenancy, and per-source access policies.
- Hybrid (BM25+vector) search, re-ranking, and query rewriting — filtering is exact metadata match only.
- Azure AI Search implementation — remains a stub per the parent plan.
- Cross-encoder or LLM-based relevance scoring of retrieved chunks.

## Constraints

- Every new source/store/filter sits behind an interface and is registered in `AddRagPlatform`; no `new`-ing adapters or `Environment.GetEnvironmentVariable` in pipeline logic — bind via `IOptions<T>`.
- Adding the S3/Blob sources MUST require **zero** changes to parsers and chunking strategies; the `IDocumentParser` contract stays path-based (cloud objects materialize to temp).
- Existing local-directory ingestion behavior and the existing `IngestionRequest(Path, Strategy)` call sites MUST keep working (back-compat: `Path` maps to a `file` source).
- Per workspace rule: every created/modified code file gets tests, and every new module/directory gets a `README.md` (purpose, usage, inputs/outputs, dependencies).
- `dotnet build -warnaserror` warning-free and `dotnet test` green before any step is marked done. **Do not claim build/test verification until `dotnet` is available and the commands have actually run** (SDK is currently absent locally per project CLAUDE.md).
- Pin all new NuGet versions explicitly; verify current AWSSDK.S3 / Azure.Storage.Blobs APIs via Context7 before coding each adapter.
- Sub-agent driven: one subagent per step below, each producing exactly one focused git commit (`feat(...)`/`refactor(...)`/`test(...)`) with a passing build for that step.

## Steps (subagent-driven, one git commit each)

> **Model legend** — pick the cheapest capable model per step for least token usage. **Claude**: Opus 4.8 (concurrency/interface design), Sonnet 4.6 (standard adapter implementation), Haiku 4.5 (small well-scoped edits, docs, tests). **Codex**: `gpt-5-codex (high)` for design-heavy steps, `gpt-5-codex (medium)` for standard.

| Step | Deliverable | Commit | Claude | Codex |
|---|---|---|---|---|
| **S1 — Source abstraction + local adapter** | `IDocumentSource`, `SourceItem`, `IDocumentSourceResolver`, `LocalDirectorySource`; refactor `IngestionPipeline` enumeration to use the resolver with identical local behavior; back-compat `Path`→`file` mapping | `refactor(ingest): source connector abstraction` | Sonnet 4.6 | gpt-5-codex (medium) |
| **S2 — AWS S3 source** | `AwsS3DocumentSource` + `S3Options` + DI + pin `AWSSDK.S3`; materialize-to-temp + cleanup; `s3://` resolution | `feat(ingest): aws s3 document source` | Sonnet 4.6 | gpt-5-codex (medium) |
| **S3 — Azure Blob source** | `AzureBlobDocumentSource` + `AzureBlobOptions` + DI + pin `Azure.Storage.Blobs`; `azureblob://` resolution | `feat(ingest): azure blob document source` | Sonnet 4.6 | gpt-5-codex (medium) |
| **S4 — Background job subsystem** | `IngestionJob`/status model, `IIngestionJobStore` (in-memory), `Channel` queue, `IngestionBackgroundService` with bounded parallelism; pipeline refactor to process per-item concurrently | `feat(ingest): async background ingestion jobs` | **Opus 4.8** | gpt-5-codex (high) |
| **S5 — Scoped retrieval filter** | `VectorSearchFilter`; extend `IVectorStore.SearchAsync`; implement in InMemory + Elasticsearch; thread `Origin`/`fileType` into `DocumentMetadata` + `VectorRecord`; extend `QueryRequest` | `feat(query): metadata-scoped retrieval` | **Opus 4.8** | gpt-5-codex (high) |
| **S6 — Configurable grounded prompt** | `LlmOptions.SystemPrompt` + strong default; wire `QueryPipeline`; grounding in `DeterministicLlmClient`; `LLM_SYSTEM_PROMPT` in `.env.example` | `feat(llm): configurable grounding system prompt` | Haiku 4.5 | gpt-5-codex (medium) |
| **S7 — API + CLI surfaces** | `POST /documents`→202+jobId, `GET /jobs/{id}`; CLI `ingest <uri...> [--wait]`, `jobs status`, query filter flags | `feat(api,cli): async ingest + job status + query filters` | Sonnet 4.6 | gpt-5-codex (medium) |
| **S8 — Tests, samples, docs, compose** | Unit tests (mocked S3/Blob, job queue, filter, prompt) + integration (LocalStack + Azurite Testcontainers); per-module READMEs; docker-compose + `.env.example` | `test(ingest): multi-source + job + filter coverage` | Sonnet 4.6 + Haiku 4.5 (docs) | gpt-5-codex (medium) |

Suggested order: S1 → (S2 ∥ S3) → S4 → S5 → S6 → S7 → S8. S2 and S3 are independent after S1 and can run as parallel subagents. S5 and S6 are independent of the source work and may start any time after S1.

## Definition of Done

`dotnet build -warnaserror` is warning-free and `dotnet test` (including LocalStack S3 + Azurite Blob integration tests) is green; `POST /documents` accepting a `file`, an `s3://`, and an `azureblob://` source each return `202` with a `jobId` whose `GET /jobs/{id}` reaches `Succeeded`; and a subsequent `POST /query` carrying a source/file-type filter returns a grounded answer with at least one citation drawn only from the filtered documents, using the configurable system prompt.

## Acceptance Criteria

- **AC-1** `IDocumentSource` + `IDocumentSourceResolver` exist; `file`, `s3`, and `azureblob` URIs each resolve to a distinct adapter, and local-directory ingestion produces identical results to the pre-refactor behavior.
- **AC-2** `AwsS3DocumentSource` lists and downloads supported objects under an `s3://bucket/prefix` (verified against LocalStack) without changing any parser.
- **AC-3** `AzureBlobDocumentSource` lists and downloads supported blobs under an `azureblob://container/prefix` (verified against Azurite) without changing any parser.
- **AC-4** `POST /documents` returns `202` with a `jobId`; the request thread does not block on parse/embed.
- **AC-5** `GET /jobs/{id}` transitions `Queued`→`Running`→`Succeeded` (or `Failed` with an error message) and reports ingested document/chunk counts.
- **AC-6** A single store simultaneously holds chunks whose metadata `Origin` includes at least two of `file`/`s3`/`azureblob` and at least two file types.
- **AC-7** `IVectorStore.SearchAsync` accepts a `VectorSearchFilter`; a query filtered by `source`/`fileType`/`documentId` returns citations only from documents matching the filter.
- **AC-8** `LlmOptions.SystemPrompt` is bound from `LLM_SYSTEM_PROMPT`, sent on every chat call, and defaults to a grounding instruction; when context lacks the answer the deterministic client returns a no-answer response.
- **AC-9** Each new module directory has a `README.md`, and every new/modified code file has accompanying tests.
- **AC-10** `dotnet build -warnaserror` is warning-free and `dotnet test` passes all unit + integration tests.

## Verification

```bash
# 0. Build (warnings are errors)
dotnet build -warnaserror

# 1. Local backends incl. LocalStack (S3) + Azurite (Blob)
docker-compose up -d
#   seed: aws --endpoint-url=http://localhost:4566 s3 mb s3://rag-docs && \
#         aws --endpoint-url=http://localhost:4566 s3 cp ./samples s3://rag-docs/ --recursive
#   seed: az storage blob upload-batch --account-name devstoreaccount1 -d rag-docs -s ./samples \
#         --connection-string "$AZURITE_CONNECTION_STRING"

# 2. Async ingest from three sources via API
dotnet run --project src/Rag.Api &
JOB=$(curl -s -X POST localhost:5000/documents -H 'Content-Type: application/json' \
  -d '{"sources":["./samples","s3://rag-docs/","azureblob://rag-docs/"]}' | jq -r '.jobId')
curl -s localhost:5000/jobs/$JOB | jq '.status, .documentCount, .chunkCount'   # -> "Succeeded", >0, >0

# 3. Scoped, grounded query (only PDF documents from S3)
curl -s -X POST localhost:5000/query -H 'Content-Type: application/json' \
  -d '{"question":"What is the refund policy?","filter":{"origins":["s3"],"fileTypes":[".pdf"]}}' \
  | jq '.answer, .citations'        # citations all from s3 .pdf docs; answer grounded

# 4. CLI parity (inline job, prints jobId, waits)
dotnet run --project src/Rag.Cli -- ingest s3://rag-docs/ --wait
dotnet run --project src/Rag.Cli -- jobs status <jobId>
dotnet run --project src/Rag.Cli -- query "What is the refund policy?" --type .pdf --source s3

# 5. Full suite (unit + LocalStack + Azurite integration)
dotnet test
```

## Turn Budget

`90 turns` (aggregate across the 8 steps; ~8–14 per step as separate subagents).

> Risk note: budget may need to increase if LocalStack/Azurite Testcontainers (S8) or the background-service + bounded-parallelism refactor (S4) prove environment-sensitive.

## References

- AWS SDK for .NET — S3 — https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/s3-apis-intro.html
- Azure Storage Blobs client (.NET) — https://learn.microsoft.com/dotnet/api/overview/azure/storage.blobs-readme
- `BackgroundService` + `System.Threading.Channels` queue — https://learn.microsoft.com/dotnet/core/extensions/queue-service
- Elasticsearch kNN with filter — https://www.elastic.co/guide/en/elasticsearch/reference/current/knn-search.html
- LocalStack — https://docs.localstack.cloud/ · Azurite — https://learn.microsoft.com/azure/storage/common/storage-use-azurite
- Parent plan — [enterprise-rag-platform_2026-06-23.md](enterprise-rag-platform_2026-06-23.md)

## Risks / Open Questions

- **Parser contract vs. cloud streams.** Materialize-to-temp keeps `IDocumentParser` stable but adds temp I/O; ensure deterministic cleanup even on parse failure (try/finally per item).
- **Embedding dimension mismatch** across mixed sources still applies — all chunks must use one embedding model/dimension to match the ES `dense_vector` mapping.
- **CLI vs. API async semantics.** CLI is a short-lived process, so it runs the job inline; the `jobId` is real but completes within the command. Keep the contract identical so behavior is aligned, not the execution model.
- **AWS/Azure credentials in CI** — integration tests rely on LocalStack/Azurite with fake credentials; ensure no real cloud calls leak when endpoints are unset.
- **Bounded parallelism** must cap concurrent embedding calls to respect provider rate limits (`SemaphoreSlim`/`Parallel.ForEachAsync` with `MaxDegreeOfParallelism` from options).
```
