# ADR 004: Store consistency and single-process jobs

Status: accepted for the single-operator reference release.

## Decision

Document catalogs, chunk stores and vector indexes have explicit roles. Workbench job admission is serialized in one API process. Persisted queued/running jobs recover on restart; pause/cancel/resume are controlled transitions. Evaluation admission reserves profiles and snapshots corpus revision.

## Consequences and alternatives

Persistence does not establish distributed worker ownership or exactly-once execution. Multiple API workers are unsupported. Cross-store failures require retry/reindex and paired backups. Profile identifiers are not tenant authorization.
