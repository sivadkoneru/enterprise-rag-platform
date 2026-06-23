# Enterprise RAG Platform

A generic, plug-and-play Retrieval-Augmented Generation platform for .NET. The platform ingests `txt`, `md`, and `pdf` documents, parses and chunks them, stores documents and chunk metadata in a document store, indexes vectors, and answers grounded questions through separately configured embedding and chat endpoints.

This repository is being built from [plans/enterprise-rag-platform_2026-06-23.md](plans/enterprise-rag-platform_2026-06-23.md). Async multi-source ingestion work is tracked in [plans/async-multisource-ingestion_2026-06-23.md](plans/async-multisource-ingestion_2026-06-23.md).

## Status

The solution targets `.NET 10` through `Directory.Build.props`; this machine currently has .NET SDK `10.0.301`.

Implemented surfaces in the current tree:

- `Rag.Core` contracts, models, parser adapters, chunking strategies, LLM clients, document stores, vector stores, pipelines, and DI.
- `Rag.Api` minimal API endpoints with Swagger.
- `Rag.Cli` commands for ingest, chunk preview, query, and config display.
- Unit and integration test projects.
- Local Docker Compose services for Elasticsearch, MongoDB, LocalStack S3, and Azurite Blob Storage.

## Stack

- .NET 10 and C# with nullable reference types enabled.
- ASP.NET Core minimal APIs for the Web API.
- System.CommandLine for the CLI.
- Microsoft.Extensions.DependencyInjection, Configuration, Options, and Http.Resilience for composition, configuration, and outbound resilience.
- UglyToad.PdfPig for PDF text extraction.
- Markdig for Markdown parsing.
- MongoDB.Driver and Microsoft.Azure.Cosmos for document store adapters.
- Elasticsearch vector retrieval through the `IVectorStore` adapter.
- HTTP OpenAI-compatible embedding/chat endpoints plus a deterministic local LLM client.
- xUnit, FluentAssertions, and Testcontainers for tests.
- Docker Compose for local Elasticsearch, MongoDB, LocalStack S3, and Azurite Blob Storage.

## Repository Layout

```text
src/
  Rag.Core/        Core contracts, models, adapters, strategies, pipelines, and DI extension
  Rag.Api/         ASP.NET Core Web API
  Rag.Cli/         Command-line interface
tests/
  Rag.Core.Tests/        Unit tests for parsing, chunking, pipelines, LLM, and configuration
  Rag.Integration.Tests/ Integration/provider tests and backend container coverage
samples/           Sample txt, md, and pdf inputs
plans/             Implementation plan
docker-compose.yml Local Elasticsearch, MongoDB, LocalStack S3, and Azurite
```

## Architecture

The design is adapter-first and dependency-injection-first:

- Parsers implement `IDocumentParser` and are resolved by extension/content type.
- Chunkers implement `IChunkingStrategy`; supported strategies are `fixed`, `recursive`, `markdown-aware`, and `semantic`.
- LLM providers expose separate `IEmbeddingClient` and `IChatClient` contracts.
- Document stores implement `IDocumentStore`; current providers are `memory`, `file`, `mongo`, and `cosmos`.
- Vector stores implement `IVectorStore`; current providers are `memory` and `elasticsearch`.
- Azure AI Search is represented only by interface/stub coverage for a future phase.
- Source adapters resolve `file`, `s3`, and `azureblob` URIs behind source interfaces; cloud objects materialize to local temporary files before parser resolution.
- Pipelines orchestrate parse -> chunk -> embed -> persist and query -> retrieve -> hydrate -> prompt -> answer with citations.
- Async ingestion jobs enqueue source URIs, report `Queued`/`Running`/`Succeeded`/`Failed`, and expose document/chunk counts through a job store.
- Retrieval filters can scope vector search by source URI, origin (`file`, `s3`, `azureblob`), document id, and file type.
- `AddRagPlatform(configuration)` is the composition root for platform services.

External dependencies sit behind interfaces. Runtime selection comes from configuration bound to options classes, with environment variables taking precedence over JSON settings.

## Configuration

The API reads `appsettings.json`, `appsettings.{Environment}.json`, a local `.env` file if present, environment variables, and command-line arguments. The CLI reads a local `.env` file if present and environment variables. Use [.env.example](.env.example) as the starter template:

```bash
cp .env.example .env
```

Key selectors:

| Variable | Values | Default |
|---|---|---|
| `LLM_PROVIDER` | `deterministic`, `openai`, `azure-openai` | `deterministic` |
| `DOC_STORE` | `memory`, `file`, `mongo`, `cosmos` | `memory` |
| `VECTOR_STORE` | `memory`, `elasticsearch` | `memory` |
| `CHUNKING_STRATEGY` | `fixed`, `recursive`, `markdown-aware`, `semantic` | `fixed` for core/CLI defaults; `recursive` in API `appsettings.json` and `.env.example` |

Common options:

| Variable | Purpose |
|---|---|
| `CHUNK_SIZE` | Target chunk size. |
| `CHUNK_OVERLAP` | Target overlap between chunks. |
| `SEMANTIC_DISTANCE_THRESHOLD` | Semantic chunk boundary threshold. |
| `SEMANTIC_CHUNKING_LLM_REFINEMENT_ENABLED` | Enables optional chat-based semantic boundary refinement. |
| `LLM_API_KEY` | API key for HTTP LLM providers. |
| `LLM_EMBEDDING_ENDPOINT` | Embedding endpoint URL. |
| `LLM_EMBEDDING_MODEL` | Embedding model/deployment name. |
| `LLM_EMBEDDING_DIMENSIONS` | Embedding vector dimension. |
| `LLM_CHAT_ENDPOINT` | Chat completion endpoint URL. |
| `LLM_CHAT_MODEL` | Chat model/deployment name. |
| `LLM_SYSTEM_PROMPT` | Grounding prompt sent on every chat call. |
| `INGESTION_MAX_PARALLELISM` | Maximum concurrent source items processed by background ingestion. |
| `S3_REGION` | AWS region for S3 source enumeration. |
| `S3_ENDPOINT` | Optional S3 endpoint override, for example LocalStack. |
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | Credentials for S3 sources; local defaults use fake LocalStack values. |
| `AZURE_BLOB_ENDPOINT` | Optional Azure Blob endpoint override, for example Azurite. |
| `AZURE_BLOB_CONNECTION_STRING` | Connection string for Azure Blob sources. |
| `MONGO_CONNECTION_STRING` | MongoDB connection string. |
| `MONGO_DATABASE` | MongoDB database name. |
| `MONGO_CHUNKS_COLLECTION` | MongoDB chunks collection name. |
| `COSMOS_CONNECTION_STRING` | Cosmos DB connection string. |
| `COSMOS_ENDPOINT` / `COSMOS_KEY` | Cosmos DB endpoint/key alternative. |
| `COSMOS_DATABASE` | Cosmos DB database name. |
| `COSMOS_CHUNKS_CONTAINER` | Cosmos DB chunks container name. |
| `ELASTICSEARCH_URI` | Elasticsearch endpoint, for example `http://localhost:9200`. |
| `ELASTICSEARCH_INDEX` | Elasticsearch index name. |
| `ELASTICSEARCH_VECTOR_DIMENSIONS` | Elasticsearch vector dimension. |

Example local `.env` for MongoDB and Elasticsearch:

```dotenv
LLM_PROVIDER=deterministic
DOC_STORE=mongo
MONGO_CONNECTION_STRING=mongodb://localhost:27017
MONGO_DATABASE=rag
MONGO_CHUNKS_COLLECTION=chunks
VECTOR_STORE=elasticsearch
ELASTICSEARCH_URI=http://localhost:9200
ELASTICSEARCH_INDEX=rag-chunks
ELASTICSEARCH_VECTOR_DIMENSIONS=1536
CHUNKING_STRATEGY=recursive
```

## Local Workflow

```bash
# Build warning-free
dotnet build -warnaserror

# Start local backing services when using MongoDB or Elasticsearch
docker-compose up -d

# Configure local environment when selecting non-default providers
cp .env.example .env

# Show effective provider selections without secrets
dotnet run --project src/Rag.Cli -- config

# Preview chunking strategies
dotnet run --project src/Rag.Cli -- chunk:preview ./samples/handbook.pdf

# Ingest and query through the CLI
dotnet run --project src/Rag.Cli -- ingest ./samples
dotnet run --project src/Rag.Cli -- ingest ./samples s3://rag-docs/ azureblob://rag-docs/ --wait
dotnet run --project src/Rag.Cli -- query "What is the refund policy?"
dotnet run --project src/Rag.Cli -- query "What is the refund policy?" --source s3 --type .pdf

# Run the API
dotnet run --project src/Rag.Api
```

## API Surface

- `GET /health` returns service health.
- `POST /documents` enqueues ingestion for one or more `file`, `s3`, or `azureblob` sources and returns `202 Accepted` with a `jobId`.
- `GET /jobs/{id}` returns queued/running/succeeded/failed ingestion job state, counts, document ids, and any error.
- `POST /chunk/preview` previews all chunking strategies for a document path.
- `POST /query` runs retrieval and answer generation using `QueryRequest`; scoped filters can target `sources`, `origins`, `documentIds`, and `fileTypes`.

Swagger is enabled by default when the API runs.

## CLI Surface

- `ingest <uri...> [--wait] [--strategy <name>]` ingests one or more local paths or source URIs.
- `jobs status <id>` reports the async ingestion job status known to the current process.
- `chunk:preview <path>` compares chunking strategies for a document.
- `query <question> [--source <value>] [--document <id>] [--type <extension>]` runs retrieval and answer generation with optional filters.
- `config` prints provider selections without secrets.

## Testing

```bash
dotnet test
```

Integration tests that use real backing services may require:

```bash
docker-compose up -d
```

LocalStack and Azurite are included for S3 and Azure Blob ingestion coverage. Seed examples:

```bash
aws --endpoint-url=http://localhost:4566 s3 mb s3://rag-docs
aws --endpoint-url=http://localhost:4566 s3 cp ./samples s3://rag-docs/ --recursive
az storage blob upload-batch -d rag-docs -s ./samples --connection-string "$AZURE_BLOB_CONNECTION_STRING"
```

Do not mark an implementation epic complete until the relevant build and test commands have actually passed.
