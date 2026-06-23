# Claude Handoff

This repository is collaborative, so check the working tree before edits and never overwrite unrelated changes.

## Product Intent

Create a generic enterprise RAG platform in .NET. The system ingests `txt`, `md`, and `pdf` documents from local files, S3 prefixes, and Azure Blob prefixes, normalizes text, chunks content, embeds chunks, stores documents and chunks in a document database, indexes vectors in Elasticsearch, and answers questions with grounded citations through configurable LLM endpoints and `LLM_SYSTEM_PROMPT`.

## Repository Structure

```text
src/Rag.Core/              Core abstractions, models, adapters, strategies, pipelines, and DI
src/Rag.Providers.Aws/     AWS S3 document source (opt-in: AddRagAwsS3)
src/Rag.Providers.AzureBlob/ Azure Blob document source (opt-in: AddRagAzureBlob)
src/Rag.Providers.Cosmos/  Cosmos DB document store (opt-in: AddRagCosmos)
src/Rag.Providers.Mongo/   MongoDB document store and job store (opt-in: AddRagMongo)
src/Rag.Api/               ASP.NET Core API: async ingest, jobs, chunk preview, query, health, Swagger
src/Rag.Cli/               CLI: ingest, jobs status, chunk:preview, query, config
tests/Rag.Core.Tests/      Unit tests
tests/Rag.Cli.Tests/       CLI command and exit-code tests
tests/Rag.Integration.Tests/ Integration/provider tests (Docker required, except AdapterSelectionTests)
samples/                   Sample txt, md, and pdf inputs
```

`Rag.Core` must stay free of provider SDK dependencies. Backends live in `Rag.Providers.*`, register
themselves as keyed services, and are wired by an explicit `AddRag*` call; requesting a provider
whose package is not referenced must fail with a message naming the missing package.

## Design Constraints

- Adapter pattern for parsers, document stores, vector stores, embedding clients, and chat clients.
- Strategy pattern for chunking.
- Factory/resolver patterns for parser and chunking selection.
- Options pattern for all environment-bound configuration.
- DI-first composition through `AddRagPlatform(configuration)`.
- No direct provider SDK construction or environment reads in core pipeline logic.
- No Azure AI Search implementation in the current scope; only interface/stub coverage is allowed.
- Source adapters for `file`, `s3`, and `azureblob` must sit behind resolver contracts and keep parser contracts path-based.
- Retrieval filters must be exact metadata filters and must not require query pipeline changes when a vector backend is swapped.
- Grounding system prompts must come from options, not pipeline literals.

## Runtime Selectors

- `LLM_PROVIDER`: `deterministic`, `openai`, or `azure-openai`.
- `DOC_STORE`: `memory`, `file`, `mongo`, or `cosmos`.
- `VECTOR_STORE`: `memory` or `elasticsearch`.
- `CHUNKING_STRATEGY`: `fixed`, `recursive`, `markdown-aware`, or `semantic`.
- `LLM_SYSTEM_PROMPT`: grounding instruction sent with every chat request.

The deterministic LLM client and memory stores are the default local path. HTTP LLM providers require separate embedding and chat endpoints. LocalStack and Azurite are the local defaults for S3 and Azure Blob source testing.

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
- Keep adapters replaceable through registration and options.
- Prefer explicit options objects and validation.
- Keep CLI and API behavior aligned.
- Return citations from query flows.
- Pin package versions centrally in `Directory.Packages.props`.
- Treat build warnings as errors.
- Add focused tests for source changes and update module READMEs when creating or changing module responsibilities.

## Verification

The current local environment has .NET SDK `10.0.301` available.

```bash
dotnet build enterprise-rag-platform.sln -warnaserror
dotnet test enterprise-rag-platform.sln --filter "Category!=Integration"
```

`tests/Rag.Integration.Tests` starts LocalStack, Azurite, MongoDB, and Elasticsearch through
Testcontainers, so a bare `dotnet test` needs a running Docker daemon. Without one those tests fail
with an explicit message naming the prerequisite rather than a container stack trace. Use the
`Category!=Integration` filter above when Docker is unavailable.

For backend-dependent checks:

```bash
docker-compose up -d
dotnet test enterprise-rag-platform.sln
```

Do not claim build, test, Docker, provider, or end-to-end verification unless the commands were actually run in this session.
