# Jobs

The jobs module provides asynchronous ingestion execution for API callers.

`IIngestionJobQueue` writes queued jobs to a `System.Threading.Channels` channel, `IngestionBackgroundService` drains that queue, and `IIngestionJobStore` tracks job state. The default store is in-memory and intended for local/API process lifetime only. Set `JOB_STORE=mongo` to persist job state in MongoDB and recover queued or interrupted running jobs when the API restarts.

Inputs are `IngestionRequest` values. Outputs are `IngestionJob` records with status, live source/document/chunk counts, document IDs, chunk IDs, timestamps, worker ownership, and errors. Dependencies are `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, channels, MongoDB when persistent jobs are enabled, and the core ingestion pipeline.
