# Rag.Integration.Tests

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
