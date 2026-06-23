# Enterprise RAG Platform

A generic, plug-and-play Retrieval-Augmented Generation platform for .NET. The platform ingests `txt`, `md`, and `pdf` documents, parses and chunks them, stores chunk metadata in a NoSQL document store, indexes vectors in Elasticsearch, and answers grounded questions through separately configured embedding and chat endpoints.

This repository is currently being built from [plans/enterprise-rag-platform_2026-06-23.md](plans/enterprise-rag-platform_2026-06-23.md).

## Stack

- .NET 10 target, matching the SDK installed on this machine.
- ASP.NET Core for the Web API.
- System.CommandLine for the CLI.
- Microsoft.Extensions.DependencyInjection, Configuration, Options, and Http.Resilience for composition, configuration, and outbound resilience.
- UglyToad.PdfPig for PDF text extraction.
- Markdig for Markdown parsing.
- MongoDB or Cosmos DB selected through the `IDocumentStore` contract. The current adapters are file-backed placeholders with the same DI selection boundary.
- Elasticsearch selected through the `IVectorStore` contract. The current adapter writes index metadata locally and uses in-process cosine search until the SDK-backed adapter is completed.
- Azure OpenAI or OpenAI for embeddings and chat completions.
- xUnit, FluentAssertions, and Testcontainers for unit and integration tests.
- Docker Compose for local Elasticsearch and MongoDB.

## Architecture

The core design is adapter-first and dependency-injection-first:

- Parsers implement `IDocumentParser` and are resolved by extension or content type.
- Chunkers implement `IChunkingStrategy`; supported strategies are fixed, recursive, markdown-aware, and semantic.
- LLM providers expose separate embedding and chat clients so embedding and answer-generation endpoints can be configured independently.
- Document stores implement `IDocumentStore`; memory, file-backed, Mongo-selected, and Cosmos-selected adapters are registered.
- Vector stores implement `IVectorStore`; memory and Elasticsearch-selected adapters are registered, with an Azure AI Search interface stub only.
- Pipelines orchestrate parse -> chunk -> embed -> persist and query -> retrieve -> hydrate -> prompt -> answer with citations.
- `AddRagPlatform(configuration)` is the composition root for all platform services.

External dependencies should sit behind interfaces. Runtime selection should come from environment variables bound to options classes, not from scattered direct environment reads.

## Repository Layout

```text
src/
  Rag.Core/        Core contracts, models, adapters, strategies, pipelines, and DI extension
  Rag.Api/         ASP.NET Core API endpoints
  Rag.Cli/         Command-line interface
tests/
  Rag.Core.Tests/
  Rag.Integration.Tests/
samples/           Sample txt, md, and pdf inputs for preview and end-to-end tests
plans/             Source implementation plan
```

## Configuration

Copy `.env.example` to `.env` and fill in provider-specific values before running ingestion, query, or integration tests.

Key selectors:

- `LLM_PROVIDER`: `openai` or `azure-openai`
- `DOC_STORE`: `mongo` or `cosmos`
- `VECTOR_STORE`: `elasticsearch`
- `CHUNKING_STRATEGY`: `fixed`, `recursive`, `markdown-aware`, or `semantic`

Embedding and chat settings are intentionally separate so deployments can use different models, endpoints, keys, and deployment names.

## Local Workflow

```bash
# Restore and build
dotnet build -warnaserror

# Start local backing services
docker-compose up -d

# Configure local environment
cp .env.example .env

# Preview chunking strategies
dotnet run --project src/Rag.Cli -- chunk:preview ./samples/handbook.pdf

# Ingest and query through the CLI
dotnet run --project src/Rag.Cli -- ingest ./samples
dotnet run --project src/Rag.Cli -- query "What is the refund policy?"

# Run the API
dotnet run --project src/Rag.Api
curl -s localhost:5000/health

# Run tests
dotnet test
```

Local verification note: `dotnet` is not available on this machine's PATH at the time these docs were created, so the commands above are documented from the plan but were not executed locally.

## API Surface

- `POST /documents` to ingest documents.
- `POST /chunk/preview` to compare chunking strategies.
- `POST /query` to answer grounded questions with citations.
- `GET /health` to report service health.

## CLI Surface

- `ingest <path>` to ingest a file or directory.
- `chunk:preview <path>` to compare chunking strategies for a document.
- `query <question>` to run retrieval and answer generation.
- `config` to display effective provider and storage configuration without secrets.

## Implementation Notes

- The deterministic LLM provider is the default so local ingest/query flows can run without secrets.
- OpenAI and Azure OpenAI can be selected with `LLM_PROVIDER=openai` or `LLM_PROVIDER=azure-openai` plus separate embedding and chat endpoint variables.
- The Azure AI Search vector store is intentionally a stub, matching the plan's future-phase constraint.
- The SDK is installed at `/usr/local/share/dotnet/dotnet`; use that path directly if `dotnet` is not on `PATH`.

## Development Rules

- Keep business logic free of direct provider SDK construction.
- Bind all provider settings through options classes.
- Keep provider-specific code in adapters.
- Do not commit secrets or local `.env` files.
- Add or update module README files when creating module directories.
- Run `dotnet build -warnaserror` and `dotnet test` before marking implementation epics complete, once the .NET SDK is available.
