# Rag.LiveEvals

A thin measured-evaluation client for the existing `/api/v1/queries` workbench. It does not implement
another RAG pipeline and does not alter the deterministic `Rag.Evals` harness.

## Inputs and workflow

1. Review `../live/dataset.json` and its six synthetic text documents. Seed labels are unreviewed
   **drafts**, not established ground truth. There are 20 development and 50 held-out cases; 15 held-out cases
   require abstention. Review reference facts and source-qualified spans, freeze the labels, set status
   to `frozen`, and version/hash the dataset before the final run. Tune only on development cases.
2. In a private environment, configure one real embedding/chat pair, an output ceiling, and a recursive
   profile. Ingest only this corpus. Record indexing usage/cost separately and include that estimate in
   the total run budget. Do not exceed the $10 allocation while indexing.
3. Copy `../live/run.example.json` outside version control. Supply actual model IDs, corpus/profile IDs,
   source commit, dated USD prices and indexing cost. Zero placeholder prices are rejected. The runner
   reserves indexing cost first and conservative per-request estimates (including configured retries)
   before query/judge calls. Reservations are never refunded. Provider billing caps are separate;
   this estimate is not a financial guarantee. Configure the same provider output ceiling on the API.
4. Export `RAG_API_KEY` and optionally `RAG_EVAL_JUDGE_KEY` privately. Never put credentials in JSON or
   shell history. An optional chat-completions judge endpoint/model supplies claim-level estimates.
5. From the repository root:

   ```bash
   dotnet run --project evals/Rag.LiveEvals -c Release -- /private/path/run.json evals/live/dataset.json /private/path/new-run
   ```

   The output directory must not already exist. The runner rejects deterministic providers, mismatched
   models, unfrozen datasets, invalid spans, corpus hash mismatches and unversioned documents.
   It compares vector/hybrid at Top K 5 with no reranker, one request at a time and no excluded warm-up.

## Outputs and interpretation

- `manifest.json`: schema version, commit, dataset/corpus hashes, profile, provider/model identity,
  query settings, generation-prompt hash, pricing, scorer/judge versions, runtime/OS, timing method
  and limitations. Archive the matching operator-owned prompt with private run configuration.
- `cases.jsonl`: every requested outcome, raw answer/evidence/citations, scores, usage, failures and
  budget skips. Never publish private content.
- `aggregate.json` and generated `summary.md`: retrieval, generation, reference resolution, latency,
  abstention confusion matrix and estimated resource cost. Null is missing, not zero.
- `human-review.jsonl`: fixed first-five sample plus failed/disputed cases. Add independent reviewer
  judgments and calculate agreement before publishing semantic conclusions.

Recall@K is source-qualified evidence-anchor recall; MRR@K locates the first fully supported anchor.
NDCG@K uses 0/1/2 relevance with an ideal ranking from the same profile's judged chunk set; partial
matching is conservative and needs human judgment coverage review. Unanswerable cases are excluded.
Faithfulness counts substantive claims supported by admitted context. Correctness combines claim
precision and required-fact recall (F1), with contradiction rate. Citation support and claim citation
coverage are separate from reference resolution. Semantic abstention uses judge assessment of
substantive answers, including refusal-plus-answer cases. No judge means these metrics remain null.

Latency uses measured nearest-rank p50/p95 with count, failures and stage timings. Usage comes from
provider responses where available; context characters/4 is not billed prompt usage. Final-success
usage does not include failed retry usage: cost reports retain conservative reservations separately.
Indexing and judging are separate cost categories. Missing usage is explicit.

Dependencies: .NET 10, Rag.Core contracts, configured private API; optional external judge. Ordinary
PR checks exercise scorers and budgeting with fixtures and never make paid calls. No measured baseline
is checked in until an actual run and review are completed.
