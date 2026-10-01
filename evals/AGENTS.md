# Evaluation Harness

- `Rag.Evals/Corpus/HandbookContent.cs` is the source for `samples/handbook.pdf`; do not edit the
  PDF directly.
- Keep handbook quantities unique and excluded terms absent.
- Keep scoring deterministic; do not read `LLM_PROVIDER` from the environment.
- After score-affecting changes, regenerate the report and keep `evals/results/` and README tables
  aligned.

See [README.md](README.md) for metric details.
