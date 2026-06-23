# Plan — Enterprise RAG Platform (.NET, plug-and-play)

> Generated 2026-06-23 via `/plan-for-goal`. Built to be executed epic-by-epic with subagents.

## Brief

A generic, plug-and-play Retrieval-Augmented Generation engine in .NET that ingests `txt`/`md`/`pdf` documents through pluggable parser adapters, chunks them via a selectable strategy, stores chunks+metadata in a NoSQL store (Cosmos DB or MongoDB) with vectors in Elasticsearch, and answers grounded queries through env-configured embedding + chat endpoints — all wired with DI so any adapter (e.g. Azure AI Search later) can be swapped in without touching the core.

## Stack

- .NET 10 (LTS), C# 14 — fallback .NET 8 (LTS) if 10 unavailable in target env
- ASP.NET Core (Web API surface)
- System.CommandLine (CLI surface)
- Microsoft.Extensions.DependencyInjection / .Configuration / .Options (DI + env binding)
- Microsoft.Extensions.Http.Resilience (Polly v8) — retries/timeouts on outbound calls
- UglyToad.PdfPig (PDF text extraction, MIT)
- Markdig (Markdown parsing)
- Elastic.Clients.Elasticsearch 8.x (vector store + cosine kNN)
- MongoDB.Driver (Mongo document store)
- Microsoft.Azure.Cosmos (Cosmos DB document store)
- Azure.AI.OpenAI 2.x + OpenAI .NET SDK (one adapter, both backends; separate embedding + chat endpoints)
- xUnit + FluentAssertions + Testcontainers (.NET) — Elasticsearch + Mongo; Cosmos DB emulator
- Docker Compose (local Elasticsearch + MongoDB)

## Architecture & Design Patterns

- **Adapter** — `IDocumentParser`, `IDocumentStore`, `IVectorStore`, `IEmbeddingClient`, `IChatClient`. Each external concern hides behind an interface; concrete adapters are registered by env config.
- **Strategy** — `IChunkingStrategy` (Fixed, Recursive, MarkdownAware, optional Semantic), selected at runtime.
- **Factory** — `IDocumentParserResolver` (by extension/content-type), `IChunkingStrategyFactory`, store/provider factories that pick the adapter from `*_PROVIDER` env vars (keyed DI services).
- **Options pattern** — every adapter reads a strongly-typed `IOptions<T>` bound from environment variables; no `Environment.GetEnvironmentVariable` scattered in logic.
- **Pipeline** — `IIngestionPipeline` (parse → chunk → embed → persist chunk + vector) and `IQueryPipeline` (embed query → vector search → fetch chunks → build prompt → chat → cite).
- **Repository** — document stores expose repository semantics over chunk + metadata documents.
- **Dependency Injection everywhere** — single `services.AddRagPlatform(configuration)` extension composes the whole graph; no `new` of adapters in business logic.

```
src/
  Rag.Core/        # abstractions, strategies, adapters, pipelines, DI extension
  Rag.Api/         # ASP.NET Core Web API (ingest, chunk preview, query, health)
  Rag.Cli/         # console: ingest, chunk:preview, query, config
tests/
  Rag.Core.Tests/        # unit
  Rag.Integration.Tests/ # Testcontainers (ES, Mongo) + Cosmos emulator
samples/           # sample .txt / .md / .pdf for chunk preview + e2e
docker-compose.yml # local Elasticsearch + MongoDB
```

## Scope — Functionality

1. Parse `txt`, `md`, `pdf` into normalized text + metadata via extension-resolved parser adapters.
2. Provide four chunking strategies behind `IChunkingStrategy` — Fixed-size+overlap, Recursive, Markdown-aware, and Semantic (embedding-distance boundary detection via the embedding endpoint, with optional LLM boundary refinement via the chat endpoint).
3. CLI `chunk:preview <path>` runs every strategy over sample docs and prints a comparison table (strategy, chunk count, avg size, overlap, sample) for the user to choose; `CHUNKING_STRATEGY` env var, when set, skips the prompt.
4. Generate embeddings via an env-configured **embedding endpoint** and answers via a separate env-configured **chat endpoint**; one `ILlmProvider` adapter supports both Azure OpenAI and OpenAI, selected by `LLM_PROVIDER`.
5. Persist chunks + metadata to a NoSQL document store selected by `DOC_STORE` (`cosmos` | `mongo`).
6. Index chunk vectors in Elasticsearch and perform cosine-similarity kNN retrieval via `IVectorStore`; `VECTOR_STORE` selects the adapter.
7. Ingestion pipeline orchestrates parse → chunk → embed → persist (doc store + vector store) idempotently.
8. Query pipeline embeds the question, retrieves top-k vectors, hydrates chunks from the doc store, builds a grounded prompt, calls the chat endpoint, and returns the answer with source citations.
9. Expose the above via Web API endpoints (`POST /documents`, `POST /chunk/preview`, `POST /query`, `GET /health`) and via CLI commands.
10. Single `AddRagPlatform(configuration)` DI extension composes all adapters from env config.

## Epics (with suggested Claude + Codex models)

> Suggested models: **Claude** — Opus 4.8 for design/algorithm-heavy work, Sonnet 4.6 for standard implementation, Haiku 4.5 for scaffolding/docs/tests. **Codex** — `gpt-5-codex (high)` for complex epics, `gpt-5-codex (medium)` for standard. Use these per subagent.

| Epic | Deliverable | Claude | Codex |
|---|---|---|---|
| **E0 — Solution & DI foundation** | Solution + 5 projects, core abstractions/interfaces, `AddRagPlatform`, Options binding from env, keyed-service provider selection skeleton | Sonnet 4.6 | gpt-5-codex (medium) |
| **E1 — Document parser adapters** | `IDocumentParser` + resolver factory; Txt, Markdown (Markdig), Pdf (PdfPig) adapters; normalized text + metadata model | Sonnet 4.6 | gpt-5-codex (medium) |
| **E2 — Chunking strategies + preview** | `IChunkingStrategy` + factory; Fixed/Recursive/MarkdownAware + **Semantic** (embedding-distance boundaries, optional LLM refinement); CLI `chunk:preview` comparison report; env override. *Semantic strategy depends on E3 (`IEmbeddingClient`/`IChatClient`).* | Opus 4.8 | gpt-5-codex (high) |
| **E3 — LLM provider adapter** | `IEmbeddingClient` + `IChatClient`; one adapter over Azure OpenAI + OpenAI; **separate** embedding/chat endpoints; Polly resilience | Sonnet 4.6 | gpt-5-codex (high) |
| **E4 — NoSQL document stores** | `IDocumentStore` repository; Cosmos DB + MongoDB adapters; chunk+metadata persistence; `DOC_STORE` selection | Sonnet 4.6 | gpt-5-codex (medium) |
| **E5 — Elasticsearch vector store** | `IVectorStore`; ES `dense_vector` + cosine kNN adapter; index management; **Azure AI Search interface stub only — not implemented** | Opus 4.8 | gpt-5-codex (high) |
| **E6 — Ingestion + query pipelines** | `IIngestionPipeline` + `IQueryPipeline` orchestration; grounded prompt builder; citations | Opus 4.8 | gpt-5-codex (high) |
| **E7 — Web API surface** | ASP.NET Core endpoints (documents, chunk/preview, query, health) + Swagger | Sonnet 4.6 | gpt-5-codex (medium) |
| **E8 — CLI surface** | System.CommandLine: ingest, chunk:preview, query, config | Haiku 4.5 | gpt-5-codex (medium) |
| **E9 — Tests, samples, docs, compose** | Unit + Testcontainers integration tests, sample docs, per-module README, docker-compose | Sonnet 4.6 (integration) + Haiku 4.5 (docs) | gpt-5-codex (medium) |

Suggested order: E0 → E1 → E3 → E2 → E4 → E5 → E6 → E7/E8 (parallel) → E9. E3 now precedes E2 because the Semantic strategy consumes the embedding/chat clients. E4/E5 remain independent after E0 and can run as parallel subagents; E2's non-semantic strategies can also start before E3 and add Semantic once E3 lands.

## Out of Scope

- **Azure AI Search adapter** — interface/stub only in E5; no implementation (explicit future phase).
- Document formats beyond `txt`/`md`/`pdf` (docx, html, images/OCR) — adapter pattern leaves room, not built now.
- AuthN/AuthZ, multi-tenancy, rate limiting, and API gateway concerns.
- UI / front-end — the API is headless; no web pages or visual components.
- Re-ranking, hybrid (BM25+vector) search, query rewriting, and agentic multi-hop retrieval.
- Fine-tuning, model hosting, or local-embedding models — all inference is via the configured endpoints.
- Production infra (Terraform/Helm/CI-CD pipelines) beyond local docker-compose.

## Constraints

- Every external dependency MUST sit behind an interface and be registered via DI; no `new`-ing adapters or `Environment.GetEnvironmentVariable` inside business logic (use `IOptions<T>`).
- Adding a new parser / store / vector backend MUST require zero changes to `Rag.Core` pipelines — only a new adapter + registration.
- All configuration via environment variables (documented in README + `.env.example`); no secrets committed.
- No Azure AI Search implementation — stub interface only.
- Per workspace rule: every created/modified code file gets tests, and every new module/directory gets a `README.md` (purpose, usage, inputs/outputs, dependencies).
- `dotnet build` warning-free (treat warnings as errors in CI) and `dotnet test` green before any epic is marked done.
- Pin NuGet versions explicitly; verify current package APIs via Context7 before coding each adapter.

## Definition of Done

Running `docker-compose up` then `dotnet test` passes all unit + integration tests, and the CLI (and `POST /query`) ingests the `samples/` `txt`/`md`/`pdf` files through the env-selected parser → chunker → embedding endpoint → NoSQL store + Elasticsearch, and returns a grounded answer with at least one source citation — with provider/store/vector adapters swappable purely via environment variables.

## Acceptance Criteria

- **AC-1** Solution builds warning-free with `Rag.Core`, `Rag.Api`, `Rag.Cli`, and two test projects.
- **AC-2** `txt`, `md`, and `pdf` files each parse to normalized text via their extension-resolved adapter.
- **AC-3** Four chunking strategies (Fixed, Recursive, MarkdownAware, Semantic) are selectable; `CHUNKING_STRATEGY` env var overrides the interactive prompt.
- **AC-4** `chunk:preview` prints a comparison table across all strategies for a given document.
- **AC-5** Embeddings and chat use two separately configured endpoints, and `LLM_PROVIDER` switches between Azure OpenAI and OpenAI without code changes.
- **AC-6** `DOC_STORE=mongo` and `DOC_STORE=cosmos` each persist and retrieve chunks via the same `IDocumentStore` contract.
- **AC-7** Elasticsearch adapter indexes vectors and returns top-k results ranked by cosine similarity.
- **AC-8** End-to-end query over the sample docs returns an answer containing source citations.
- **AC-9** An `IVectorStore` (Azure AI Search) stub exists with no implementation, proving the swap point — and no Azure Search SDK call is made.
- **AC-10** Integration tests run against Testcontainers (Elasticsearch + Mongo) and pass via `dotnet test`.

## Verification

```bash
# 1. Build
dotnet build -warnaserror

# 2. Spin up local backends
docker-compose up -d        # Elasticsearch + MongoDB

# 3. Configure (example — Mongo + OpenAI)
cp .env.example .env        # set LLM_PROVIDER, EMBED_*, CHAT_*, DOC_STORE=mongo, VECTOR_STORE=elasticsearch

# 4. Preview chunking strategies on samples
dotnet run --project src/Rag.Cli -- chunk:preview ./samples/handbook.pdf

# 5. Ingest + query via CLI
dotnet run --project src/Rag.Cli -- ingest ./samples
dotnet run --project src/Rag.Cli -- query "What is the refund policy?"

# 6. Query via API
dotnet run --project src/Rag.Api &
curl -s localhost:5000/health
curl -s -X POST localhost:5000/query -H 'Content-Type: application/json' \
  -d '{"question":"What is the refund policy?"}' | jq '.answer, .citations'

# 7. Full test suite (unit + Testcontainers integration)
dotnet test
```

Swap check: re-run step 5 with `DOC_STORE=cosmos` (Cosmos emulator) and confirm identical behavior with no rebuild of `Rag.Core`.

## Turn Budget

`100 turns` (aggregate across all epics; ~8–12 per epic when run as separate subagents).

> Risk note: budget may need to increase if Cosmos emulator / Testcontainers setup (E4, E9) or the dual-backend LLM adapter (E3) prove environment-sensitive.

## References

- UglyToad.PdfPig — https://github.com/UglyToad/PdfPig
- Markdig — https://github.com/xoofx/markdig
- Elastic .NET client (dense_vector / kNN) — https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/index.html
- Azure.AI.OpenAI .NET — https://learn.microsoft.com/dotnet/api/overview/azure/ai.openai-readme
- Testcontainers for .NET — https://dotnet.testcontainers.org/

## Risks / Open Questions

- **Semantic chunking** is in scope (E2) and calls the embedding endpoint per document, adding token cost and latency at ingest; cache embeddings during boundary detection and keep `chunk:preview` opt-in for the semantic pass to bound cost.
- **Cosmos DB testing** depends on the emulator (Linux emulator can be flaky in CI); MongoDB integration is the primary verified path.
- **Embedding dimension** must match the Elasticsearch `dense_vector` mapping — pin a default model/dimension in config to avoid index/embed mismatch.
- Confirm target framework: plan assumes **.NET 10 LTS**; drop to .NET 8 LTS if the toolchain isn't available.
