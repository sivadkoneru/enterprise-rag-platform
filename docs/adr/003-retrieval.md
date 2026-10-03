# ADR 003: Chunking and retrieval choices

Status: accepted for the single-operator reference release.

## Decision

Fixed, recursive, Markdown-aware and semantic chunking remain explicit choices. The first measured baseline compares vector and vector/BM25 reciprocal-rank fusion with recursive chunks and Top K 5. Reranking is measured separately.

## Consequences and alternatives

Chunk overlap trades index size for evidence continuity. RRF combines rankings without pretending heterogeneous scores are calibrated probabilities. Fixed-pool reranking cannot recover missed documents. Neighbor expansion and context limits can change admitted evidence and must be recorded.
