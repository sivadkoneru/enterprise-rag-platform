# Agent Guide

This repository is being built collaboratively from [plans/enterprise-rag-platform_2026-06-23.md](plans/enterprise-rag-platform_2026-06-23.md). Expect concurrent edits. Do not revert or overwrite work you did not create.

## Current Mission

Build a .NET RAG platform with:

- Parser adapters for `txt`, `md`, and `pdf`.
- Chunking strategies: fixed, recursive, markdown-aware, and semantic.
- Separate embedding and chat endpoints.
- NoSQL document stores: MongoDB and Cosmos DB.
- Elasticsearch vector storage and cosine kNN retrieval.
- Web API, CLI, local compose stack, samples, and tests.

## Collaboration Rules

- Work in your assigned ownership area only.
- Read the plan before implementation.
- Check the current tree before editing; this repo may change while you work.
- Never revert unrelated changes.
- Preserve docs-only, source-only, or test-only ownership boundaries when assigned.
- If a directory or module is created, add a README that explains purpose, usage, inputs/outputs, and dependencies.

## Architecture Rules

- Use dependency injection everywhere.
- Keep all external dependencies behind interfaces.
- Register provider adapters from configuration using `AddRagPlatform(configuration)`.
- Use options binding for environment configuration.
- Do not call `Environment.GetEnvironmentVariable` from business logic.
- Do not instantiate SDK adapters directly from pipelines.
- Adding a new parser, document store, or vector backend must not require core pipeline changes.

## Provider Scope

- LLM provider selector: `LLM_PROVIDER`.
- Document store selector: `DOC_STORE`.
- Vector store selector: `VECTOR_STORE`.
- Azure AI Search is a future vector backend. Create interface/stub coverage only when that epic is active; do not add an Azure Search implementation or SDK calls.

## Verification

When the .NET SDK is available:

```bash
dotnet build -warnaserror
docker-compose up -d
dotnet test
```

Local note: `dotnet --info` currently fails with `command not found` in this environment, so .NET verification cannot be run here until the SDK is installed or added to PATH.

