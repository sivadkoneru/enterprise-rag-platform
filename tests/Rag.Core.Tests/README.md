# Rag.Core.Tests

Unit tests for `Rag.Core`.

## Purpose

Cover parser resolution, chunking behavior, options validation, prompt construction, pipeline orchestration, and the async ingestion contracts that do not require live backends.

Async ingestion coverage belongs here when it can run without network access:

- Public source resolver contracts for `file`, `s3`, and `azureblob`.
- Local directory source behavior for recursive supported file discovery.
- In-memory job store and queue state transitions.
- In-memory vector filtering by source URI, origin, document id, and file type.
- `LLM_SYSTEM_PROMPT` binding and deterministic no-answer grounding.

## Dependencies

Expected test dependencies include xUnit and FluentAssertions. Tests in this project should avoid network calls and containerized services.
