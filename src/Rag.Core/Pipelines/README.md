# Rag.Core Pipelines

## Ingestion Scaling

Source items stream from the source resolver straight into a bounded `Parallel.ForEachAsync`, and
each item is disposed as soon as it is ingested. Cloud adapters materialize one temporary file per
item, so at most `INGESTION_MAX_PARALLELISM` of them exist on local disk at a time rather than the
whole prefix.

Progress snapshots copy the accumulated document and chunk id lists, so intermediate reports are
rate-limited to at most one every 500 ms. The exact final state is always reported after the loop
completes, and `TotalSourceCount` grows as sources are discovered, reaching its true value once
enumeration finishes.

Ingestion and query pipeline orchestration.

The ingestion pipeline should resolve source URIs, parse source items, chunk, embed, and persist chunks and vectors. Local paths keep the legacy `IngestionRequest.Path` behavior; multi-source requests can include `file`, `s3`, and `azureblob` URIs.

Async ingestion should enqueue jobs, return a job id immediately, and update job state through `Queued`, `Running`, `Succeeded`, or `Failed` with document/chunk counts and errors.

The query pipeline should embed the question, pass metadata filters to vector search, hydrate chunks, build a grounded prompt from `LLM_SYSTEM_PROMPT`, call chat, and return citations only from retrieved chunks.
