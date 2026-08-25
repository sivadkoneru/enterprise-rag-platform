# Rag.Evals.Tests

Tests for the evaluation harness in `evals/`.

Five concerns, in increasing order of scope:

- `AnchorResolverTests` — offset mapping, whitespace collapsing, and the two failure modes that must
  be loud: a phrase that no longer resolves, and one that resolves more than once.
- `ScorerTests` — each metric against hand-built inputs, including the boundary-split case that only
  `unionCovers` rescues.
- `HandbookCorpusTests` — the committed `samples/handbook.pdf` still matches `HandbookContent`, and
  the generator still emits paragraph breaks. The binary is committed but the generator is only
  reachable through a CLI command CI does not run, so nothing else would notice them drifting apart.
- `DatasetIntegrityTests` — the golden dataset still describes the corpus: anchors resolve, declared
  difficulty matches measured overlap, unanswerable questions stay unanswerable.
- `ChunkingBenchmarkTests` and `EvalRegressionTests` — the published benchmark is meaningful (no
  strategy collapsed to one chunk) and retrieval has not regressed below the committed floors in
  `evals/Rag.Evals/Data/thresholds.json`.

These need no Docker and are deliberately **not** tagged `[Trait("Category", "Integration")]`, so
they run everywhere including the `Category!=Integration` local loop. The full run takes about a
second: the whole evaluation is shared through one `EvalFixture` per test session.

Following `tests/README.md`, these assert behavior rather than structure. Nothing here greps a
repository file for a table — README freshness is a CI concern, enforced by regenerating the tables
and failing on a git diff.
