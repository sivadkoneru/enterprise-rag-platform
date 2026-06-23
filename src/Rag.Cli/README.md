# Rag.Cli

System.CommandLine-based command-line interface for local and operational RAG workflows.

## Purpose

Provide scriptable access to ingestion, ingestion job status, chunk preview, querying, and configuration inspection.

## Commands

- `ingest <uri...> [--wait] [--strategy <name>]`
- `jobs status <id>`
- `chunk:preview <path>`
- `query <question> [--source <value>] [--document <id>] [--type <extension>]`
- `config`

### `ingest`

Runs ingestion inline for one or more local paths or source URIs and prints a generated `jobId` for parity with the API contract.

Examples:

```bash
dotnet run --project src/Rag.Cli -- ingest ./samples --wait
dotnet run --project src/Rag.Cli -- ingest ./samples s3://rag-docs/ --strategy recursive
```

The CLI process is short-lived, so the default in-memory `IIngestionJobStore` only tracks jobs for the current invocation.

### `jobs status`

Prints the status for a job known to the current CLI process. With the default in-memory store, jobs from previous CLI invocations report `Unknown`.

### `query`

Accepts filter flags for the planned scoped retrieval surface:

```bash
dotnet run --project src/Rag.Cli -- query "What is the refund policy?" --source s3 --type .pdf
```

For CLI ergonomics, `--source` maps to source origin values such as `file`, `s3`, or `azureblob`. The flags map into the core `VectorSearchFilter` contract.

## Dependencies

Depends on `Rag.Core` and System.CommandLine. Commands should resolve services from the same DI graph as the API.
