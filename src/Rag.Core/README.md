# Rag.Core

Core platform library for parsing, chunking, embedding, persistence, retrieval, and pipeline orchestration.

## Purpose

Own stable abstractions and reusable business logic. Provider SDKs are allowed only behind adapters and registrations; pipelines should depend on interfaces.

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

Expected dependencies include Microsoft.Extensions packages, parser libraries, provider SDKs used by adapters, and resilience primitives.
