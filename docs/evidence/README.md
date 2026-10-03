# Verification evidence

The implementation was checked locally on 2026-10-03. These checks establish reference behavior,
not a production certification or a real-model quality baseline.

| Check | Observed result |
|---|---|
| .NET Release build | Zero warnings and errors |
| Non-integration tests | 208 passed |
| Docker provider integration tests | 15 passed |
| Frontend unit/DOM tests | 80 passed |
| Linux production browser suite | 8 passed; 2 private-only cases run separately |
| Actual API/private-client browser suite | 2 passed |
| Deterministic report regeneration | Existing result artifacts unchanged byte-for-byte |
| Generated Playground fixtures | Match authoritative benchmark, dataset and Compose sources |

Frontend checks include a fresh `npm ci` in the pinned Linux browser container, lint with zero
warnings, generated route types, typecheck and the default production build. The final component
extractions were rechecked against that same environment and approved screenshots. Backend provider
images were available locally; uncached image pulls and the remote GitHub workflow are still release
verification steps. This evidence is from the working tree, not a published release tag.

- .NET Release build uses warnings as errors. Unit, regression and Docker-backed provider tests run
  against the implementation; new cases cover candidate pools above ten and obsolete-vector deletion.
- Frontend install, lint, generated route types, typecheck, unit/DOM tests and the **default Turbopack
  production build** run with Node 24. Browser tests use standalone production output.
- Private-client browser tests start the actual .NET API with local deterministic models, verify
  shared-key enforcement/redaction, then ingest/query/inspect/evaluate synthetic documents.
- Linux screenshot baselines were generated and compared in
  `mcr.microsoft.com/playwright:v1.63.0-noble` (amd64). They cover cited evidence and the mobile
  refusal panel. Update them only after inspecting actual screenshots; do not auto-approve diffs.
- Compose builds both non-root images, verifies loopback exposure, ingests a fixture, restarts the API,
  verifies persistence and queries again. Its temporary project and volumes are removed afterward.

[Load smoke artifact](compose-load-2026-10-03.json): one document/two chunks, deterministic local
models, concurrency two, one warm-up, 20/20 successful queries. Observed p50 **9.16 ms**, p95
**21.35 ms** on the recorded Docker host. This tiny warm workload does not establish capacity,
external-provider performance or an SLA. It records the base commit plus tracked diff hash and
explicitly identifies an uncommitted implementation snapshot; rerun at the release commit before
using it as release performance evidence.

The production dependency audit reported no advisories at verification. The development lint chain
reported a braces/fast-glob/micromatch denial-of-service advisory; npm's suggested remedy downgrades
Next's ESLint configuration across major versions. That incompatible automatic downgrade was not
applied. Lint processes trusted repository patterns; track the upstream compatible fix before release.

No paid real-model evaluation, human label freeze, Vercel publication, remote branch protection or
release tag was performed. Those remain external release steps, not passing checks inferred from code.
