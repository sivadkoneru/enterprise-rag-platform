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

Depends on `Rag.Core` and ASP.NET Core. Provider behavior should be selected through environment configuration and DI.
