# Samples

Sample documents for chunk preview, ingestion, and end-to-end query tests.

## Purpose

Hold representative `txt`, `md`, `pdf`, and structured-data schema files used by the CLI, API, and integration tests.

## Expected Files

- Plain text sample documents.
- Markdown sample documents.
- PDF sample documents.
- JSONL/CSV schema sidecars, including a Natural Questions-style JSONL schema.

## handbook.pdf is generated

`handbook.pdf` is rendered from `evals/Rag.Evals/Corpus/HandbookContent.cs`, which is the single
source of truth for its content. Do not edit the PDF by hand; regenerate it instead:

```bash
dotnet run --project evals/Rag.Evals -- generate-handbook
```

It is a ~20 page synthetic employee handbook used by the evaluation harness in `evals/`. Its layout
constants are functional rather than cosmetic: PdfPig reconstructs paragraph breaks from baseline
gaps, and the chunking strategies that split on blank lines depend on them surviving extraction.

`handbook.md` and `handbook.txt` remain small hand-written smoke samples and are unrelated to the
evaluation corpus.

Do not store sensitive or proprietary documents here.
