# Rag.Api

ASP.NET Core Web API for the RAG platform.

## Purpose

Expose ingestion, chunk preview, query, and health endpoints over the core platform services.

## Endpoints

- `POST /documents`
- `POST /chunk/preview`
- `POST /query`
- `GET /health`

## Dependencies

Depends on `Rag.Core` and ASP.NET Core. Provider behavior is selected through configuration and DI.

## Configuration

The API reads configuration from `appsettings.json`, optional environment-specific JSON such as `appsettings.Development.json`, optional `.env`, and environment variables.

Precedence is:

1. JSON files.
2. `.env` values found in the working directory or a parent directory.
3. Environment variables.

JSON uses the same option sections registered by `AddRagPlatform`, for example `Llm:Provider`, `DocumentStore:Provider`, and `VectorStore:Provider`. Environment variables can use the flat aliases from `.env.example`, such as `LLM_PROVIDER`, `DOC_STORE`, and `VECTOR_STORE`.
