# ADR 005: Deterministic regression and measured evaluation

Status: accepted for the single-operator reference release.

## Decision

Keep the 50-question deterministic harness pinned and byte-reproducible. A separate runner invokes the existing workbench API with explicit real model identities, frozen source-qualified labels, dated prices and a capped estimated budget.

## Consequences and alternatives

Hashed embeddings and copied-answer overlap diagnose regressions, not model quality. Lexical overlap and reference resolution do not establish entailment. Semantic judge counts are estimates pending human agreement review. Missing scores are null; complete failures and budget skips remain in reports.
