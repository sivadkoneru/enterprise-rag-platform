# Evaluation Harness

The corpus and the published numbers are generated. Invariants no compiler enforces:

- `Rag.Evals/Corpus/HandbookContent.cs` is the single source of truth; `HandbookPdfWriter` renders
  `samples/handbook.pdf` from it. Never hand-edit the PDF.
- Handbook quantities must stay globally unique, and the excluded topics must stay absent — the
  unanswerable questions depend on it, and the loader asserts each `absentTerms` entry is missing
  from every ingested document, distractors included.
- Scoring is pinned to the deterministic provider inside `RunProfile.Default`, which
  `EvalHost.Build` turns into an explicit configuration dictionary. Setting `LLM_PROVIDER` around
  the harness changes nothing; do not reintroduce environment reads.
- After any change that can move scores, run `dotnet run --project evals/Rag.Evals -- report --write`
  and keep `evals/results/` and the `README.md` tables in the same commit — CI fails on drift.

See [README.md](README.md) for what these metrics do and do not measure.
