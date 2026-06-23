# Tests

Contains unit and integration tests for the enterprise RAG platform.

## Projects

- `Rag.Core.Tests`: fast unit tests for abstractions, strategies, resolvers, pipeline behavior, async ingestion contracts, vector filters, and prompt grounding.
- `Rag.Integration.Tests`: Testcontainers-backed tests for MongoDB, Elasticsearch, LocalStack S3, Azurite Blob Storage, and provider integration boundaries.

## Verification

Run when the .NET SDK is available:

```bash
dotnet test
```

Do not mark verification complete unless `dotnet test` was actually run in the current session.

Async multi-source ingestion tests should cover:

- `file`, `s3`, and `azureblob` source resolution without changing parser contracts.
- Background job store and queue state transitions.
- Vector search filters for source URI, origin, document id, and file type.
- `LLM_SYSTEM_PROMPT` binding and grounded no-answer behavior.
