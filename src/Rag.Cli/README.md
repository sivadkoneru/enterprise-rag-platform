# Rag.Cli

System.CommandLine-based command-line interface for local and operational RAG workflows.

## Purpose

Provide scriptable access to ingestion, ingestion job status, chunk preview, querying, and configuration inspection.

## Commands

- `ingest <uri...> [--strategy <name>]`
- `jobs status <id>`
- `chunk:preview <path>`
- `query <question> [--source <uri>] [--origin <origin>] [--document <id>] [--type <extension>]`
- `config`

### `ingest`

Runs ingestion inline for one or more local paths or source URIs and prints a generated `jobId` for parity with the API contract.

Examples:

```bash
dotnet run --project src/Rag.Cli -- ingest ./samples
dotnet run --project src/Rag.Cli -- ingest ./samples s3://rag-docs/ --strategy recursive
```

Ingestion always runs inline and returns when the job finishes. This is a deliberate synchronous mode:
the CLI process builds a plain `ServiceCollection`/`ServiceProvider` with no `IHost`, so it cannot run
the background `IngestionBackgroundService` the API uses (registered separately via
`AddRagIngestionWorker`, API-only). The CLI instead drives the same job-store transitions
(create -> mark-running -> ingest -> mark-succeeded/failed) directly.

The CLI process is short-lived, so the default in-memory `IIngestionJobStore` only tracks jobs for the current invocation.

### `jobs status`

Prints the status for a job known to the current CLI process. With the default in-memory store, jobs from previous CLI invocations report `Unknown`.

### `query`

Accepts filter flags for the scoped retrieval surface:

```bash
dotnet run --project src/Rag.Cli -- query "What is the refund policy?" --origin s3 --type .pdf
dotnet run --project src/Rag.Cli -- query "What is the refund policy?" --source s3://rag-docs/refund.pdf
```

Each flag maps to exactly one field of the core `VectorSearchFilter`, matching the API:

- `--source` filters by exact source URI or path.
- `--origin` filters by `file`, `s3`, or `azureblob`.
- `--document` filters by document id.
- `--type` filters by file extension, with or without the leading dot.

Filter fields are combined with AND, so passing several flags narrows the result set. The CLI builds
the filter by calling the shared `VectorSearchFilter.FromLists` factory directly (the same factory the
API's `ApiQueryFilter.ToCoreFilter` calls), so omitting every flag produces no filter and empty flag
values normalize the same way on both surfaces.

## Dependencies

Depends on `Rag.Core` and System.CommandLine. `Program.cs` also references and calls all four opt-in
provider packages (`Rag.Providers.Aws`, `Rag.Providers.AzureBlob`, `Rag.Providers.Cosmos`,
`Rag.Providers.Mongo`) right after `AddRagPlatform(...)`, matching the API, so every documented
`DOC_STORE`, `JOB_STORE`, and source scheme keeps working without extra setup. Commands should resolve
services from the same DI graph as the API.
