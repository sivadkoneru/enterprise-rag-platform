# Rag.Cli

System.CommandLine-based command-line interface for local and operational RAG workflows.

## Purpose

Provide scriptable access to ingestion, chunk preview, querying, and configuration inspection.

## Commands

- `ingest <path>`
- `chunk:preview <path>`
- `query <question>`
- `config`

## Dependencies

Depends on `Rag.Core` and System.CommandLine. Commands should resolve services from the same DI graph as the API.
