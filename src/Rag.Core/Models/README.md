# Rag.Core Models

Shared domain models for documents, chunks, metadata, embeddings, retrieval results, prompts, answers, and citations.

Models should remain provider-neutral and serializable where they cross API, CLI, or storage boundaries.

`VectorSearchFilter.FromLists(documentIds, sources, origins, fileTypes)` is the single factory for
turning caller-supplied filter lists into a `VectorSearchFilter?`: every field normalizes independently
(an empty or null list becomes `null`), and the whole filter collapses to `null` when nothing was
supplied. The API and the CLI both call it instead of each re-implementing this normalization, so
"no filter" and "empty list" mean the same thing on every surface. Request-specific concerns (such as
the API's `Documents`/`Types` alias fields) are resolved by the caller before the merged lists are
handed to this factory; the factory itself stays provider- and surface-neutral.

`IngestionRequest.SourceUris` is similarly the single place that collapses "either `Sources` or a
single `Path`" into one trimmed, blank-filtered list; the API's `ApiIngestionRequest.GetSources()`
delegates to it rather than re-implementing the same trimming rules.

