# ADR 002: Index profiles and embedding compatibility

Status: accepted for the single-operator reference release.

## Decision

Model identity, dimensions and chunking settings belong to the indexing profile. Query and ingestion validate active identity. Ordinary source documents use stable workbench identities; obsolete chunks and vectors are deleted after replacement writes.

## Consequences and alternatives

There is no transaction spanning document and vector stores. Interrupted replacement must leave the profile unavailable until retry. Model migrations require a new profile; retain the prior profile for rollback. Structured source record deletion is not a general collection-sync feature.
