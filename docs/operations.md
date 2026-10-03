# Single-instance operating envelope

This release is a reference implementation for a private, single-operator environment. It does
not establish an SLA, high availability, multi-tenant isolation, exactly-once processing, or a
safe anonymous live service. Public hosting uses simulation mode only.

## Bounds and telemetry

The API accepts bodies up to 2 MB. Its global single-instance limit admits four concurrent
work requests with no waiting queue. Per-minute budgets are 60 queries, 10 provider checks,
10 ingestion requests, four evaluation submissions, and 600 metadata requests. Rejections return
429 and Retry-After. Workbench admission permits at most 32 queued/running/paused jobs and one
ingestion owner per profile. Paused evaluations continue to reserve their profiles.

The proxy reads JSON incrementally, limits reads to 15 seconds, gives metadata calls 15 seconds
and mutation/query streams five minutes, and propagates disconnects. The client bounds SSE frames.
Backend HTTP credentials never follow redirects. Store/model error bodies are omitted from public
errors. Review host HTTP logging settings: do not enable request/response-body logging.

Set `OTEL_EXPORTER_OTLP_ENDPOINT` on the API to export `Rag.Workbench` activities and metrics using
the pinned OpenTelemetry SDK. Query stages and model operations emit content-free spans; stage
duration, provider failures and provider embedding-token counters are available. Collector access
and retention are operator responsibilities. Do not export prompts, documents, keys or connection
strings. Query results carry a trace ID and model/usage provenance when available.

The query path loads profile chunks in memory. Hybrid retrieval can re-embed lexical-only candidates.
Source files are capped at 2 MB; a profile is bounded to 1,000 documents and 20,000 chunks. These are safety caps, not demonstrated capacity. Keep the first corpus small and measure memory, concurrency, errors and p50/p95 before increasing it.
No corpus-size or latency SLA has been established. A load report must name hardware, commit, corpus
hash/chunk count, models, concurrency, warm-up, sample size and failures; deterministic-model latency
is not a real-provider latency claim.

## Recovery and retention

1. Stop admission before backup. Snapshot MongoDB and Elasticsearch volumes together and retain the
   exact configuration/model identifiers. Encrypt backups and restrict operator access.
2. Restore to an isolated Compose project. Verify corpus/profile IDs, chunk counts, and a known query
   before reconnecting a browser. A mismatched store/index pair requires re-ingestion into a new profile.
3. On restart, queued/running workbench jobs are requeued; paused/canceled jobs remain controlled.
   Delivery is repeatable, not exactly once. `node scripts/compose-smoke.mjs` exercises persistence
   across an API restart using synthetic data and removes only its own project volumes afterward.
4. For a failed-provider exercise, use a private test configuration with an unreachable model endpoint.
   Confirm explicit failure, bounded time, safe errors, and successful manual retry after restoration.
5. Before cleanup, export any required reports. Stop the API, back up stores, remove the selected
   profile's catalog/documents/chunks and its exact Elasticsearch index together. No online profile
   deletion API is offered yet; avoid ad hoc partial deletion. Never delete a profile reserved by a job.

Live query history stays in page memory. Workbench evaluation reports persist answers and source
passages server-side until an operator removes them. Define report and backup retention for each
private installation. Public recordings use synthetic documents only. Source roots should be
read-only mounts: path/symlink checks are not a sandbox against a privileged concurrent filesystem
writer replacing files after validation.
