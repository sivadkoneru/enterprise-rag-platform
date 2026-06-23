# Agent Guide

This repository is being built collaboratively from [plans/enterprise-rag-platform_2026-06-23.md](plans/enterprise-rag-platform_2026-06-23.md). Treat that plan as the source of truth for scope, acceptance criteria, and epic ordering. Expect concurrent edits and never revert or overwrite work you did not create.

## Current Mission

Build a .NET RAG platform with:

- Parser adapters for `txt`, `md`, and `pdf`.
- Chunking strategies: fixed, recursive, markdown-aware, and semantic.
- Separate embedding and chat endpoints.
- NoSQL document stores: MongoDB and Cosmos DB.
- Elasticsearch vector storage and cosine kNN retrieval.
- Web API, CLI, local compose stack, samples, and tests.
- Async multi-source ingestion for `file`, `s3`, and `azureblob` sources with background job status.
- Scoped retrieval filters for source URI, origin, document id, and file type.
- Configurable grounding through `LLM_SYSTEM_PROMPT`.

## Repository Structure

```text
src/
  Rag.Core/        Contracts, models, parsing, chunking, LLM clients, stores, vector stores, pipelines, and DI
  Rag.Api/         ASP.NET Core minimal API surface
  Rag.Cli/         System.CommandLine-based developer/operator CLI
tests/
  Rag.Core.Tests/        Unit tests for core behavior
  Rag.Integration.Tests/ Integration tests for provider selection and backend containers
samples/           Sample txt, md, and pdf documents
plans/             Implementation plan and execution notes
docker-compose.yml Local Elasticsearch, MongoDB, LocalStack S3, and Azurite services
```

Each new directory or module must include a `README.md` that explains purpose, usage, inputs/outputs, and dependencies.

## Collaboration Rules

- Work in your assigned ownership area only.
- Read the plan before implementation.
- Check the current tree before editing; this repo may change while you work.
- Preserve docs-only, source-only, or test-only ownership boundaries when assigned.
- Do not perform unrelated refactors while completing a scoped task.
- Do not commit secrets, local `.env` files, generated stores, or machine-specific artifacts.

## Architecture Rules

- Use dependency injection everywhere.
- Keep all external dependencies behind interfaces.
- Register provider adapters from configuration using `AddRagPlatform(configuration)`.
- Use options binding for environment configuration.
- Do not call `Environment.GetEnvironmentVariable` from business logic.
- Do not instantiate SDK adapters directly from pipelines.
- Adding a new parser, document store, or vector backend must not require core pipeline changes.
- Adding a new document source must go through source resolver contracts and must not require parser or chunking changes.
- Query grounding must flow through options; do not hardcode system prompts in pipeline logic.

## Provider Scope

- LLM provider selector: `LLM_PROVIDER`.
- Document store selector: `DOC_STORE`.
- Vector store selector: `VECTOR_STORE`.
- Chunking selector: `CHUNKING_STRATEGY`.
- Grounding prompt: `LLM_SYSTEM_PROMPT`.
- Azure AI Search is a future vector backend. Keep interface/stub coverage only; do not add an Azure Search implementation or SDK calls in the current scope.

Current local defaults are intentionally runnable without external secrets:

- `LLM_PROVIDER=deterministic`
- `DOC_STORE=memory`
- `VECTOR_STORE=memory`
- `CHUNKING_STRATEGY=fixed` in core/CLI defaults; API `appsettings.json` and `.env.example` currently set `recursive`.

## Verification

The local environment currently has .NET SDK `10.0.301` available. When source or test behavior changes, run:

```bash
dotnet build -warnaserror
dotnet test
```

When integration behavior needs external services, also run:

```bash
docker-compose up -d
```

Then configure provider variables for MongoDB, Cosmos DB, Elasticsearch, LocalStack S3, Azurite Blob Storage, or HTTP LLM endpoints as needed before rerunning the relevant tests or workflows.
