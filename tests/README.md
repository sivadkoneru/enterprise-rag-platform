# Tests

Contains unit and integration tests for the enterprise RAG platform.

## Projects

- `Rag.Core.Tests`: fast unit tests for abstractions, strategies, resolvers, and pipeline behavior.
- `Rag.Integration.Tests`: Testcontainers-backed tests for MongoDB, Elasticsearch, and provider integration boundaries.

## Verification

Run when the .NET SDK is available:

```bash
dotnet test
```

Local note: `dotnet` is not currently available on PATH in this environment.
