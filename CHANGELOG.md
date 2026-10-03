# Changelog

## 0.1.0 — unreleased reference release

- Server-controlled public simulation and private live boundary; stricter proxy validation and errors.
- Explicit simulation, stage-event, evidence-coverage and lexical-diagnostic terminology.
- Candidate response-size correction, embedding validation and obsolete-chunk cleanup.
- Atomic single-process workbench admission, evaluation snapshots and bounded pending jobs.
- Frontend production-build, browser and actual-client CI jobs; generated fixture freshness checks.
- Separate measured-evaluation runner and draft 20/50 development/held-out synthetic dataset.
- MIT licensing, pinned SDK, architecture decisions and operating/deployment documentation.

Real-model baseline, human label review, public deployment and final portfolio recording remain
release evidence to collect. Version `0.1.0` describes a scoped reference release, not production certification.
Track the remaining [release acceptance conditions](docs/release-readiness.md) before tagging.

Use `vMAJOR.MINOR.PATCH` tags. Tag only after all required checks and evidence review; release CI
packages that tag. Keep breaking contract changes in the changelog and version report schemas
independently. Preserve existing history; no earlier demo commit needs rewriting.
