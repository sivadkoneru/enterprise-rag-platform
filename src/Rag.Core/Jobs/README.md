# Jobs

The jobs module provides asynchronous ingestion execution for API callers.

`IIngestionJobQueue` writes queued jobs to a `System.Threading.Channels` channel, `IngestionBackgroundService` drains that queue, and `IIngestionJobStore` tracks job state. The default store is in-memory and intended for local/API process lifetime only. Set `JOB_STORE=mongo` to persist job state in MongoDB and recover queued or interrupted running jobs when the API restarts.

## Where The Mongo Store Lives

`InMemoryIngestionJobStore` lives in this project and needs `JOB_STORE` unset or `memory`.
`MongoIngestionJobStore` lives in the sibling `Rag.Providers.Mongo` project, so `Rag.Core` has no
dependency on `MongoDB.Driver`; it keeps the `Rag.Core.Jobs` namespace, so existing `using`s stay
valid. A host that wants `JOB_STORE=mongo` references `Rag.Providers.Mongo` and calls
`AddRagMongo(configuration)`, which registers the store as a keyed `IIngestionJobStore` singleton
(key `"mongo"`) that `AddRagPlatform`'s resolver looks up by name. Requesting `mongo` without that
reference throws an `InvalidOperationException` naming the missing package instead of silently
falling back to `memory`. `IngestionJobTransitions` (shared terminal-status and backfill rules) is
`public` for exactly this reason: implementations of `IIngestionJobStore`, including this one, can
live outside `Rag.Core`.

## Store Conformance

Both stores must behave identically — swapping `JOB_STORE` should change durability, not semantics.
`IngestionJobTransitions` holds the rules they share (which statuses are terminal, and how a
succeeded job backfills its processed-source count), and the `IngestionJobStoreContractTests`
conformance suite in `Rag.Core.Tests` runs against an implementation to hold it to those rules. A new
store should subclass that suite rather than write its own transition tests.

## Progress Reporting

`IProgress<T>.Report` is synchronous, but the pipeline calls it from inside parallel ingestion
workers and the store write may be a remote round trip. Progress snapshots are therefore handed to a
bounded latest-value-wins channel and persisted by a single drain loop, so no worker thread blocks on
a database write and a slow store sheds intermediate snapshots instead of throttling ingestion. The
drain is always completed before a job is marked succeeded, so the final snapshot is never lost, and
a failed progress write is logged rather than failing the job.

Inputs are `IngestionRequest` values. Outputs are `IngestionJob` records with status, live source/document/chunk counts, document IDs, chunk IDs, timestamps, worker ownership, and errors. Dependencies are `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, channels, MongoDB when persistent jobs are enabled, and the core ingestion pipeline.
