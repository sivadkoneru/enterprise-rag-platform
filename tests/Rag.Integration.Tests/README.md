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
- Elasticsearch vector index creation, cosine kNN search, and metadata filters.
- LocalStack-backed S3 source enumeration and download.
- Azurite-backed Azure Blob source enumeration and download.
- End-to-end ingestion/query flow using test doubles for LLM endpoints where appropriate.
- Cosmos DB emulator coverage where practical.

## Dependencies

Expected dependencies include xUnit, FluentAssertions, Testcontainers for .NET, MongoDB, Elasticsearch, LocalStack, Azurite, and optional Cosmos emulator support.

Use `docker-compose up -d` for manual local services. Testcontainers-backed tests should use fake LocalStack credentials and the Azurite development connection string, never real cloud credentials.

## Docker Prerequisite

Every test class here except `AdapterSelectionTests` (DI wiring only, no container) starts a
Testcontainers-managed container, so a running Docker daemon is required. Each class is marked
`[Trait("Category", "Integration")]`, and container startup (in the Azurite/LocalStack fixtures and
in `BackendContainerTests`) is wrapped so an unreachable daemon fails with an explicit message naming
Docker as the prerequisite, chaining the original Testcontainers exception, rather than a raw
Testcontainers stack trace or a silent skip.

```bash
# Full run (requires Docker)
dotnet test

# Machines without Docker: exclude the container-backed suites
dotnet test --filter "Category!=Integration"
```
