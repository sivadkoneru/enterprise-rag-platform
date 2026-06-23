# Rag.Core Common

Small shared primitives with exactly one definition each.

## Purpose

These types exist because the same operation was previously written out at several call sites, where
the copies could drift apart independently. Nothing here holds business logic or state; each type is
a single pure rule that several modules need to agree on.

- `StableId` — the deterministic 24-character lowercase hex identifier used for ingested document
  ids, parsed file document ids, and structured record ids. Callers compose the input string; this
  owns only the hash. **Do not change the algorithm**: existing identifiers must stay byte-identical
  or previously indexed data is silently orphaned.
- `VectorMath` — cosine similarity, shared by the in-memory vector store (which ranks by it) and the
  semantic chunking strategy (which splits on `1 - similarity`). Returns `0` when either vector has
  zero magnitude, which the chunking strategy relies on to treat such a pair as maximally distant.
- `FileExtensions` — extension normalization, including the compound `.jsonl.gz` and `.ndjson.gz`
  suffixes that `Path.GetExtension` alone gets wrong. Note that source adapters keep the leading dot
  while parser metadata stores the extension without it; that asymmetry is deliberate and lives at
  the call sites, not here.
- `RagJson` — the shared `JsonSerializerOptions` (web defaults) used by the HTTP LLM client, the
  Elasticsearch vector store, and the file document store. Callers needing extra settings build a
  copy rather than mutating the shared instance, which is frozen once used.

## Dependencies

Framework only. Nothing in this folder may take a provider SDK or options dependency — if a helper
needs configuration, it belongs in the module that owns that configuration.
