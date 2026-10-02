# Enterprise RAG Playground

The Playground is a browser-based demonstration of the Enterprise RAG Platform. It provides a local corpus, query flow, retrieval configuration, trace inspection, evaluation views, and an interactive architecture map. The demonstration runs with deterministic fixtures and does not require a .NET API, LLM endpoint, API key, or Docker service.

## Run locally

Requirements: Node.js 24 and npm. The application and test tooling are verified on Node.js 24.

```bash
cd src/Rag.Playground
npm ci
npm run dev
```

Open the local URL printed by Next.js. Demo mode is local and needs no API key or model credentials. Use **Integration setup** to configure a client deployment and switch to **Client Environment** only when you want the browser to check or use a running backend.

## Available commands

```bash
npm run dev       # Start the local development server
npm run build     # Create a production build
npm run start     # Serve the production build
npm run typecheck # Check TypeScript types
npm run lint      # Run ESLint
npm run test      # Run unit tests
npm run test:e2e  # Run Playwright browser tests
```

For Vercel, set the project root directory to `src/Rag.Playground`; the build and install commands use the app's `package.json` there. For the local .NET API plus MongoDB/Elasticsearch stack, see [`docs/client-deployment.md`](../../docs/client-deployment.md).

Before running browser tests, install their browser once with `npx playwright install chromium`. Tests start a local server on port 3100 when one is not already running.

## Demo data and measurements

Corpus documents, chunks, and query runs are deterministic local fixtures. Demo response timing describes the browser-side simulation and does not represent production API, embedding, database, or model latency. Displayed published evaluation results are copied from the repository's generated deterministic benchmark report; they are not recomputed from the Playground corpus or from the current query settings. The benchmark measures retrieval and citation behavior against its own golden dataset. Its deterministic provider does not assess the quality of answers from a production language model.

The demo does not make external model calls. Its reranking experiments, context budget, neighbor expansion, standalone citation diagnostics, trace, and timing are local demonstrations. Core `POST /query` accepts a question, `topK`, and optional filters, and returns answer text with citations; the client workbench `POST /api/v1/queries` also supports configured query controls and streams trace events. Live setup uses same-origin server routes and keeps `RAG_API_KEY` out of browser code. The API key is opt-in for general API deployments; the supplied Compose profile requires one. Configure API keys and HTTP LLM credentials on the server. Demo scores and timings are not production latency or model-quality measurements.

## Client deployment

The Integrations page includes a guided configuration catalog, read-only API readiness/capability checks, and downloads for an environment template and .NET settings file. Secret fields are never accepted in the browser and remain blank in downloads. Fill credentials in a private `.env.client` on the host, then launch the local deployment using the instructions in [`docs/client-deployment.md`](../../docs/client-deployment.md). The setup page also embeds the client corpus, indexing-profile, ingestion, and document workflow.

Client queries use `lib/live/gateway.ts` and the versioned API through `app/api/client/[...path]/route.ts`. The proxy reads `RAG_API_URL` and `RAG_API_KEY` at runtime on the server. Individual query answers stay in page memory unless exported. Evaluation reports persist generated answers and evidence in `DOC_STORE`, while ingestion and evaluation job status follows `JOB_STORE`. The selected environment mode is a session preference, while published benchmarks remain independent of client evaluation results.

To exercise the complete browser workflow against a running Compose installation, mount `tests/fixtures` as the client document directory and run:

```bash
CLIENT_E2E=1 CLIENT_FIXTURE_PATH=/documents/client-policy.md \
  PLAYWRIGHT_BASE_URL=http://127.0.0.1:3000 npm run test:e2e
```

This creates a test corpus, ingests the fixture, verifies answer citations and hybrid retrieval, runs an evaluation, and checks responsive layouts. External model, reranker, and cloud-storage integrations require their own configured endpoints and credentials; the credential-free workflow uses deterministic embeddings and extractive answers.

## Main areas

- `app/` contains Next.js routes and application-wide styling.
- `features/` contains query, evaluation, architecture, and About views.
- `lib/contracts/` defines the gateway and platform data contracts.
- `lib/demo/corpus.ts` builds the stable, local demonstration corpora.
- `lib/evaluation/fixtures/` contains the checked-in deterministic evaluation data.
- `components/` contains shared layout and UI primitives.
