# Reference release acceptance

The implementation remains an **unreleased 0.1.0 reference candidate**. Code and local verification
do not establish production operation. See [observed verification results](evidence/README.md).

| Area | Delivered in the working tree | Release condition |
|---|---|---|
| Public boundary | Default-deny server mode, hidden live switch, sentinel-credential regression | Verify the same behavior on the actual Vercel preview |
| Private execution | Hardened proxy, bounded API workloads, actual ingestion/query/evaluation smoke | Keep the service behind its documented private network boundary |
| Retrieval and ingestion | Candidate count, vector validation, embedding identity and obsolete-chunk cleanup | Reindex existing experimental profiles if their embedding/content identity is uncertain |
| Job ownership | Serialized admission, capacity, snapshots, resume validation, recovery/timeout tests | One API process only; persisted records are not distributed leases |
| Evaluation | Deterministic artifacts preserved; separate real-model runner, source-span scorers, budget reservations and report provenance | Review/freeze draft labels, configure private models and dated pricing, then run and review complete results |
| Frontend | Extracted execution/setup/filter hooks, answer/detail panels and charts; runtime validation and bounded SSE parsing | Keep browser, visual and private-client checks required |
| Repository | MIT, changelog/version metadata, first-class Playground docs, five ADRs and operations runbook | Review focused commits, preserve history, tag only the accepted release |
| Portfolio | Three actual simulation screenshots and a short recording | Add measured report and explicitly labeled private HTTP-model recording before making quality/cost claims |
| CI | Seven named gates, production browser output, diagnostic artifacts and release dependencies | Confirm remote Linux runs, branch protection, uncached dependency/image availability and Vercel project settings |

## Evidence still to collect

1. Independent review of the draft 20 development / 50 held-out dataset and its source spans, including
   15 held-out abstention cases. Freeze before the final measurement.
2. A private real embedding/chat run with an operator-controlled budget and dated prices, optional
   semantic judging, and human review. The runner reserves an operator-supplied indexing estimate;
   it cannot retroactively enforce spending during earlier ingestion. Provider billing caps are separate.
3. A restore from backup exercise, beyond the automated API-restart persistence smoke. The operating
   guide describes the procedure; a successful backup restoration is not claimed by the restart test.
4. A dated workload report at the exact release commit for any performance claim. The included tiny
   deterministic smoke is diagnostic evidence only. Telemetry currently exports backend query/stage/model/
   job instrumentation and correlates proxy requests; it is not a complete production monitoring system.
5. A verified public preview/production URL, rollback target and remote required checks. No credentials,
   public URL, release tag, model score or deployment success should be inferred from this repository.

Public live access, tenant authorization, distributed workers, high availability and Kubernetes are
deliberately outside this release. Page components still compose several sections; the extractions
separate execution, filtering, transport and chart responsibilities without adding a global state framework.
