# Claude Handoff

Use [plans/enterprise-rag-platform_2026-06-23.md](plans/enterprise-rag-platform_2026-06-23.md) as the source of truth.

## Product Intent

Create a generic enterprise RAG platform in .NET. The system should ingest documents, normalize text, chunk content, embed chunks, store chunk metadata in a document database, index vectors in Elasticsearch, and answer questions with grounded citations through configurable LLM endpoints.

## Design Constraints

- Adapter pattern for parsers, document stores, vector stores, embedding clients, and chat clients.
- Strategy pattern for chunking.
- Factory/resolver patterns for parser and chunking selection.
- Options pattern for all environment-bound configuration.
- DI-first composition through `AddRagPlatform(configuration)`.
- No direct provider SDK construction or environment reads in core pipeline logic.
- No Azure AI Search implementation in the current scope; only an interface/stub is planned.

## Epic Order

1. E0: Solution and DI foundation.
2. E1: Parser adapters.
3. E3: LLM provider adapter.
4. E2: Chunking strategies and preview.
5. E4: NoSQL document stores.
6. E5: Elasticsearch vector store.
7. E6: Ingestion and query pipelines.
8. E7/E8: API and CLI surfaces.
9. E9: Tests, samples, docs, and compose.

## Quality Bar

- Keep interfaces small and stable.
- Keep adapters replaceable.
- Prefer explicit options objects and validation.
- Keep CLI and API behavior aligned.
- Return citations from query flows.
- Pin package versions explicitly when project files are created.
- Treat build warnings as errors in CI.

## Environment Note

`dotnet` is currently unavailable in this local environment (`command not found`). Do not claim build or test verification until the SDK is available and commands have actually been run.

