# Jobs

The jobs module provides in-process asynchronous ingestion execution for API callers.

`IIngestionJobQueue` writes queued jobs to a `System.Threading.Channels` channel, `IngestionBackgroundService` drains that queue, and `IIngestionJobStore` tracks job state. The default store is in-memory and intended for local/API process lifetime only.

Inputs are `IngestionRequest` values. Outputs are `IngestionJob` records with status, counts, document IDs, chunk IDs, timestamps, and errors. Dependencies are `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, channels, and the core ingestion pipeline.
