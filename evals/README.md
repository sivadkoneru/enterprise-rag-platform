# Evaluation Harness

A 50-question golden dataset over `samples/handbook.pdf`, deterministic scoring, and a side-by-side
benchmark of all four chunking strategies. Results are published in the root `README.md` and
committed under `evals/results/`.

## Layout

```text
Rag.Evals/            Console runner: corpus generator, dataset loader, scorers, report writer
Rag.Evals/Data/       golden.json (the dataset) and thresholds.json (CI regression floors)
corpus/distractors/   Small documents with deliberately conflicting policy numbers
results/              Committed artifacts: benchmark.md and latest.json
```

Tests live in `tests/Rag.Evals.Tests`.

## Commands

```bash
dotnet run --project evals/Rag.Evals -- generate-handbook   # rebuild samples/handbook.pdf
dotnet run --project evals/Rag.Evals -- dump-text --ruler   # parsed text with offsets, for authoring anchors
dotnet run --project evals/Rag.Evals -- validate            # resolve every anchor, report measured difficulty
dotnet run --project evals/Rag.Evals -- run                 # score all four strategies, print tables
dotnet run --project evals/Rag.Evals -- report --write      # regenerate results/ and the README tables
```

## What this measures, and what it does not

Scoring runs on `LLM_PROVIDER=deterministic`, so CI needs no API key and every number is
reproducible. The deterministic chat client answers with a sentence copied out of the retrieved
context, which means these metrics describe **retrieval and citation quality** — the thing chunking
actually controls — and not answer fluency or reasoning. That is a deliberate bound, stated here so
the numbers are not read as something they are not.

## The corpus

`samples/handbook.pdf` is generated, not hand-edited. `Rag.Evals/Corpus/HandbookContent.cs` is the
single source of truth; `HandbookPdfWriter` renders it. Three properties are load-bearing:

- **Every quantity is globally unique**, so each lookup question has exactly one correct evidence
  span. "thirty days" means refunds and nothing else.
- **Terms collide across sections on purpose** — "receipt" appears in both the refund and expense
  rules, "approval" in both equipment purchases and spending thresholds — so retrieval has to
  discriminate rather than match a word that occurs once.
- **The excluded topics never appear.** Equity, parental leave, dental cover, relocation, visa
  sponsorship, on-call pay, sabbaticals and pensions are absent by design, which is what makes the
  unanswerable questions genuinely unanswerable. The loader asserts their absence.

The two files in `corpus/distractors/` state conflicting numbers (sixty-day refunds, a five hundred
dollar purchase limit, net thirty terms). Without them, "did the citation point at the right
document" is satisfied for free by a single-document corpus.

The PDF's layout constants are functional. PdfPig reconstructs structure from baseline gaps, so the
line leading and paragraph spacing decide whether the extracted text contains blank lines at all.
`HandbookPdfWriter` asserts those inequalities at generation time, because losing them collapses
markdown-aware and semantic chunking to one chunk per document.

## Dataset schema

```jsonc
{
  "id": "q-014",
  "question": "What spending level requires director approval?",
  "type": "lookup",              // lookup | synthesis | unanswerable
  "difficulty": "medium",        // must match measured lexical overlap
  "expectedSourceFile": "handbook.pdf",
  "goldAnchors": [               // verbatim phrases from the parsed corpus
    { "phrase": "Director approval is required for any commitment above five thousand dollars",
      "section": "7.1 Approval Thresholds" }
  ],
  "answerKeywords": ["five thousand dollars"],
  "absentTerms": [],             // unanswerable questions only
  "notes": "Competes with the equipment threshold in 4.3."
}
```

**Gold evidence is a phrase, never a chunk id.** Chunk ids are `{documentId}:{strategy}:{index}`,
where the strategy name differs per strategy and the document id is derived from an absolute
filesystem path — so a chunk id means something different on every machine and under every strategy.
A phrase is stable across both and is resolved to character offsets at load time, which is what lets
the same label be scored against four different chunkings.

Anchors are authored by copy-paste from `dump-text` output, never from the generator source: PDF
rendering and extraction round-trip whitespace, so only the extracted form is authoritative.

## Metrics

Retrieved chunks arrive in citation order, which is descending by score. `k` is 5.

Coverage is judged two ways:

- `covers(chunk, anchor)` — one chunk contains the whole phrase. This is authoritative, because a
  chunk is what gets pasted into the prompt as a single block.
- `unionCovers(chunks, anchor)` — the retrieved chunks jointly span the phrase's character range
  without a gap. Fixed-size chunking cuts mid-sentence, so evidence routinely straddles a boundary;
  ignoring this would punish a retriever that returned everything needed. Reported separately as
  `Fragmented`, since evidence split across two blocks is genuinely worse than evidence delivered
  whole.

| Metric | Definition |
| --- | --- |
| `Recall@k` | Mean over answerable questions of the share of anchors found within the top k. |
| `Full-cov@5` | Share of questions where **every** anchor was found. Recall alone hides half-answered synthesis questions. |
| `MRR@5` | Mean reciprocal rank of the first chunk that contains an anchor outright. |
| `Citation acc@1` | Top citation names the expected file **and** its chunk contains an anchor. |
| `Citation prec@5` | Share of returned citations whose chunk contains an anchor. Punishes over-retrieval. |
| `Citation integrity` | Invariant, not a score. Every citation hydrates and its index and document match its chunk. Must be 1.000. |
| `Groundedness` | Share of answer content tokens present in the retrieved context, after stripping the deterministic client's fixed prefix. |
| `Answer-kw` | All answer keywords present. Under the deterministic client this measures chunk **boundary placement**: whether the top chunk starts at the answer. |
| `Index embed calls` | Embedding calls made during ingestion. Semantic chunking embeds every paragraph, so its index cost is structurally higher. |
| `Mean z` / `z separation` | How far the best chunk stands out from the field, in standard deviations, for answerable versus unanswerable questions. |

### Two metrics that need caveats

**Groundedness is 1.000 by construction** under the deterministic client, which answers with a
sentence copied from the context and therefore cannot emit an ungrounded token. It is a regression
tripwire — it drops if the pipeline ever sends a prompt that disagrees with its citations — and it
becomes a real measurement once a live model is configured.

**Abstention is a harness policy, not product behavior.** The platform does not abstain today:
`InMemoryVectorStore` applies no score floor and the query pipeline always builds a non-empty
context, so the deterministic client can never answer "I don't know". Scoring abstention on answer
text would therefore be a permanent zero. Instead the harness measures separability with a z-score,
which is scale-free — raw cosine similarity falls as chunks grow, so a strategy emitting large
chunks would otherwise look worse without retrieving worse. Read `z separation` rather than
`Abstention acc`: the accuracy figure depends on where the global threshold is drawn, and it flatters
a strategy that scores everything highly.

### Difficulty is measured, not assigned

`overlap(q)` is the share of a question's content tokens that also appear in its evidence phrase;
`easy >= 0.60`, `medium >= 0.30`, `hard` below that. The default embedding client is a hashed bag of
words with no stemming — "vendors" and "vendor" land in different buckets — so vocabulary overlap
genuinely predicts retrievability. `DatasetIntegrityTests` asserts every declared difficulty matches
its measured band, so the labels cannot drift away from the questions.

A zero overlap is not a broken label. Retrieval scores whole chunks, not bare anchor phrases, so a
question sharing no token with its evidence sentence can still be found through the surrounding
prose. Those are the most interesting questions in the set: whether that surrounding context lands in
the same chunk is exactly what the four strategies disagree about.

## Reproducibility

The committed artifacts must be byte-stable, because CI regenerates them and fails on any diff.
Everything is rounded to four decimal places, formatted with invariant culture, and written with
explicit `\n` line endings (the repository has no `.gitattributes`, so `Environment.NewLine` would
produce CRLF on Windows and a permanently dirty tree). No timestamps are written. Ingestion runs
single-threaded so chunk indices are identical run to run.

`EvalHost` builds its configuration from an explicit dictionary and deliberately does **not** call
`EnvFile.LoadFromWorkingDirectory`, which the CLI and API use. That helper walks up the directory
tree and would pick up a developer's gitignored `.env`, silently pointing the eval at a live LLM
endpoint and making local numbers disagree with CI's for reasons invisible in the diff.

The generated PDF is not byte-stable across PdfPig versions, so nothing diffs the binary. Corpus
integrity is guarded by anchor resolution instead, which is a stronger invariant and does not
false-positive on a package bump.

## Relationship to `chunk:preview`

`ChunkPreviewService` reports per-strategy chunk counts and average sizes for the first document in a
path, with a short sample. It answers "what does this look like"; the eval answers "which one
retrieves better". They are not duplicates and neither is built on the other.

## Adding a question

1. Add the fact to `HandbookContent.cs` if the corpus does not already state it, keeping every
   quantity globally unique.
2. `generate-handbook`, then `dump-text --ruler`.
3. Copy the evidence phrase verbatim out of that output into `Data/golden.json`.
4. `validate` — confirm the anchor resolves once and note the measured difficulty band.
5. Set `difficulty` to the measured band, then `dotnet test tests/Rag.Evals.Tests`.
6. `report --write` and commit the regenerated tables.
