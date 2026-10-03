# Portfolio assets and 50-second walkthrough

Positioning: **RAG reference platform with inspectable answers, provider integrations and reproducible
evaluation.** Do not claim production readiness, semantic accuracy or performance without a dated run.

The first three actual simulation assets and [short recording](demo/portfolio/simulation.webm) are available.
Use five readable viewport crops, with provenance visible inside each crop:

1. **[Answer and evidence](demo/portfolio/answer-evidence.png):** refund-policy question, three short cited statements and one highlighted
   source passage; retain “Demo simulation.” Place this hero directly below the README demo links.
2. **[Retrieval diagnosis](demo/portfolio/retrieval-diagnosis.png):** relevant direct policy versus conflicting reseller policy, selected candidate,
   rank change and threshold exclusion; retain “Illustrative retrieval/reranker score.”
3. **[Unsupported question](demo/portfolio/unsupported-question.png):** a question absent from the prepared corpus, refusal and no citations.
4. **Evaluation evidence:** complete held-out vector/hybrid report, sample count, p50/p95, estimated cost
   and one failed case. **Pending real-model run and human review; do not substitute an all-100% one-case report.**
5. **Private live execution:** sanitized ingestion/query recording, actual provider identifiers, reference
   resolution and measured trace. Label the recording private and state whether models are deterministic
   or HTTP providers. Never expose keys, private documents or internal hostnames.

Record at a readable desktop viewport (roughly 1440×1000) and crop to the relevant panel. Avoid tall
full-page captures. Mobile screenshots supplement these five; they should not displace buyer evidence.
Keep marketing captures in `docs/demo/portfolio/` and Playwright snapshots under tests.

| Time | Action |
|---|---|
| 0–5s | State the document-answering problem and identify the public simulation |
| 5–16s | Run the refund query and show the cited answer |
| 16–25s | Open a citation and inspect its supporting passage |
| 25–32s | Run an unsupported query and show the refusal |
| 32–42s | Show the measured report and one quality/cost tradeoff, once available |
| 42–50s | Explicitly switch to a labeled private-live recording; finish with the repository link |

Link the video beside the README hero after it exists. Before real measurements are available, publish
only the shorter simulation walkthrough and state its scope. GitHub holds package boundaries,
configuration tables, formulas, CI logs and ADRs; Upwork emphasizes workflow, sources, measured
reliability and integration capability. No placeholder should be presented as finished evidence.
