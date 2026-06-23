# Tests

Contains unit and integration tests for the enterprise RAG platform.

## Projects

- `Rag.Core.Tests`: fast unit tests for parsing, chunking, source resolution and path rules, LLM
  clients, vector filters, pipeline behavior, configuration binding, and prompt grounding.
- `Rag.Integration.Tests`: Testcontainers-backed tests for MongoDB, Elasticsearch, LocalStack S3,
  and Azurite Blob Storage, plus provider selection boundaries.

## Conventions

Assert behavior, not structure. A test should fail because the platform does the wrong thing, not
because a member was renamed — the compiler already catches renames, and a reflection or
string-matching assertion only repeats that check later and less clearly.

In practice:

- Call the type under test directly instead of locating members by name through reflection.
- Assert on observable results: returned values, emitted requests, persisted state, thrown
  exceptions.
- When guarding a repository file against drift, verify the property that matters rather than the
  presence of a substring. `EnvExampleBindingTests` probes every key in `.env.example` through
  `AddRagPlatform` and fails if the key changes no bound option, which catches dead configuration
  that a text search cannot.

## Coverage Notes

Async multi-source ingestion is covered by:

- `DocumentSourceResolverTests`: `file`, `s3`, and `azureblob` URIs select the matching adapter and
  unsupported schemes are rejected.
- `S3DocumentSourceTests` and `AzureBlobDocumentSourceTests`: prefix enumeration, download,
  schema-sidecar discovery, temp-file cleanup, and end-to-end ingest plus filtered query against
  LocalStack and Azurite.
- `LocalSourceAllowedRootsTests`: allowed-root enforcement, traversal, and symlink escape.
- `IngestionStreamingTests`: source items are processed and released one at a time.
- `AsyncIngestionContractTests`: job store and queue state transitions, progress, and cooperative
  pause.
- `VectorFilterContractTests`: filters for source URI, origin, document id, and file type.
- `PromptGroundingTests`: `LLM_SYSTEM_PROMPT` binding and grounded no-answer behavior.

The cloud source suites share the `rag-ingest` temporary directory, so they are pinned to a single
non-parallel xUnit collection (`CloudSourceCollection`). Keep new cloud suites in that collection.

## Verification

Run when the .NET SDK is available:

```bash
dotnet test
```

Integration tests require Docker: `Rag.Integration.Tests` starts MongoDB, Elasticsearch, LocalStack,
and Azurite via Testcontainers. Those test classes carry `[Trait("Category", "Integration")]`, and on
a machine without Docker they fail with an explicit message naming Docker as the prerequisite instead
of a raw Testcontainers stack trace. On such a machine, exclude them instead:

```bash
dotnet test --filter "Category!=Integration"
```

Do not mark verification complete unless the relevant `dotnet test` command was actually run in the
current session.
