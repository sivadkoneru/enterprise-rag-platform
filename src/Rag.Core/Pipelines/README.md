# Rag.Core Pipelines

Ingestion and query pipeline orchestration.

The ingestion pipeline should resolve source URIs, parse source items, chunk, embed, and persist chunks and vectors. Local paths keep the legacy `IngestionRequest.Path` behavior; multi-source requests can include `file`, `s3`, and `azureblob` URIs.

Async ingestion should enqueue jobs, return a job id immediately, and update job state through `Queued`, `Running`, `Succeeded`, or `Failed` with document/chunk counts and errors.

The query pipeline should embed the question, pass metadata filters to vector search, hydrate chunks, build a grounded prompt from `LLM_SYSTEM_PROMPT`, call chat, and return citations only from retrieved chunks.
