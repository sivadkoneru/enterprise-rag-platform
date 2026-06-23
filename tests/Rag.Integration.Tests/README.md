# Rag.Integration.Tests

> Compose-file and `.env.example` text assertions used to live here. They asserted that files
> contained certain substrings, which passes even when the stack is broken. Backing services are now
> verified by starting them (`BackendContainerTests`), and `.env.example` is verified by binding
> every documented key through `AddRagPlatform` (`EnvExampleBindingTests` in `Rag.Core.Tests`).

Integration tests for provider adapters and end-to-end RAG flows.

## Purpose

Verify document-store and vector-store behavior against real local services, primarily through Testcontainers.

## Coverage

- MongoDB document store.
- Elasticsearch vector index creation and kNN search.
- End-to-end ingestion/query flow using test doubles for LLM endpoints where appropriate.
- Cosmos DB emulator coverage where practical.

## Dependencies

Expected dependencies include xUnit, FluentAssertions, Testcontainers for .NET, MongoDB, Elasticsearch, and optional Cosmos emulator support.
