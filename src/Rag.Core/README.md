# Rag.Core

Core platform library for parsing, chunking, embedding, persistence, retrieval, and pipeline orchestration.

## Purpose

Own stable abstractions and reusable business logic. This project has no provider SDK dependency; provider adapters and SDKs belong in `Rag.Providers.*`. Pipelines depend on interfaces.

## Responsibilities

- Document parser contracts and resolver.
- Source resolver contracts and adapters for local files, S3 prefixes, and Azure Blob prefixes.
- Chunking strategy contracts and implementations.
- LLM embedding and chat contracts.
- Document store and vector store contracts.
- Ingestion and query pipelines.
- Background ingestion job contracts, in-memory job state, and queue orchestration.
- Options models and `AddRagPlatform(configuration)` registration.

## Dependencies

Dependencies include Microsoft.Extensions packages, parser libraries, and resilience primitives. Elasticsearch uses raw HTTP and introduces no provider SDK.


## Live workbench

`AddRagWorkbench(configuration)` adds hosted own-corpus ingestion and evaluation jobs, immutable
indexing profiles, detailed measured queries, and the configuration-bound `Query` and `Reranker`
options. The API exposes these services under `/api/v1`; see
[its contracts and evaluation JSON](../Rag.Api/README.md#live-workbench-api-apiv1).

`WorkbenchCatalog` persists corpus/profile/document metadata through `IWorkbenchStateStore` and
uses the configured document store for actual chunks. `WorkbenchVectorStores` gives every live
Elasticsearch profile a separate index and dimensions; the original platform index is unchanged.
`WorkbenchIngestor` reuses source adapters, parsers, chunking strategies, embedding clients, and
store adapters while counting actual indexing embedding operations.

`DetailedQueryPipeline` supports real vector retrieval, Elasticsearch BM25 or local BM25 with RRF,
HTTP reranking, normalized vector thresholds, bounded context with neighbors, actual model answers,
and citation-ID validation. `IChatUsageClient` optionally exposes provider token counts while
preserving the original `IChatClient` string response. Token estimates and the lexical groundedness
proxy are labeled explicitly. Live result exports snapshot inputs, profile/query settings, source
revision fingerprints, complete answers/evidence, and measured trace durations.

Memory/file state adapters are built in. Mongo/Cosmos state implementations remain in their provider
projects and register keyed services. Document/catalog/results use `DOC_STORE`; job state uses
`JOB_STORE`. An unavailable provider registration fails explicitly instead of falling back to memory.
The existing ingestion/query contracts and deterministic evaluation harness retain their behavior.
