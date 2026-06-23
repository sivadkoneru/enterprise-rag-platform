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

The ingestion pipeline should parse, chunk, embed, and persist chunks and vectors. The query pipeline should embed the question, retrieve vectors, hydrate chunks, build a grounded prompt, call chat, and return citations.

