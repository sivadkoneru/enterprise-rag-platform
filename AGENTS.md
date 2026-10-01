# Repository Guidance

This repository is collaborative. Check the working tree before editing and never overwrite
unrelated changes.

## Product intent

This is a generic enterprise RAG platform in .NET. It ingests `txt`, `md`, and `pdf` documents
from local files, S3 prefixes, and Azure Blob prefixes; normalizes and chunks content; embeds
chunks; stores documents and chunks in a document database; indexes vectors in Elasticsearch;
and answers questions with grounded citations through configurable LLM endpoints and
`LLM_SYSTEM_PROMPT`.

## Repository structure

```text
src/Rag.Core/                 Core abstractions, models, adapters, strategies, pipelines, and DI
src/Rag.Providers.Aws/        AWS S3 document source (opt-in: AddRagAwsS3)
src/Rag.Providers.AzureBlob/  Azure Blob document source (opt-in: AddRagAzureBlob)
src/Rag.Providers.Cosmos/     Cosmos DB document store (opt-in: AddRagCosmos)
src/Rag.Providers.Mongo/      MongoDB document store and job store (opt-in: AddRagMongo)
src/Rag.Api/                  ASP.NET Core API
src/Rag.Cli/                  CLI
tests/Rag.Core.Tests/         Unit tests
tests/Rag.Cli.Tests/          CLI command and exit-code tests
tests/Rag.Evals.Tests/        Evaluation harness tests and retrieval regression floors
tests/Rag.Integration.Tests/  Integration/provider tests
evals/Rag.Evals/              Golden dataset, deterministic scorers, benchmarks, and reports
samples/                      Sample txt, md, and pdf inputs
```

See `README.md` for architecture, runtime selectors, and configuration keys.

## Invariants

- `Rag.Core` takes no provider SDK dependency. Backends belong in `Rag.Providers.*`, register as
  keyed services, and are wired by an explicit `AddRag*` call.
- Selecting a provider whose package is not referenced must fail with an error naming the missing
  package; it must never silently fall back to in-memory.
- Configuration binds to options classes. Pipeline logic must not read environment variables;
  grounding prompts come from options, not literals.
- Package versions are pinned centrally in `Directory.Packages.props`.
- Never hand-edit generated artifacts: `samples/handbook.pdf`, `evals/results/`, and the
  evaluation tables in `README.md`. Regenerate them instead.
- Each project directory under `src/`, `tests/`, and `evals/` keeps a `README.md` describing its
  purpose, usage, inputs/outputs, and dependencies.

## Verification

The repository uses .NET SDK `10.0.301`; builds treat warnings as errors.

```bash
dotnet build enterprise-rag-platform.sln -warnaserror
dotnet test enterprise-rag-platform.sln --filter "Category!=Integration"
```

Integration tests require Docker:

```bash
docker-compose up -d
dotnet test enterprise-rag-platform.sln
```

To regenerate published evaluation tables:

```bash
dotnet run --project evals/Rag.Evals -- report --write
git diff --exit-code -- README.md evals/results
```
