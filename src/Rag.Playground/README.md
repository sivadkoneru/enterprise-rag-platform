# Enterprise RAG Playground

The Playground is a browser-based demonstration of the Enterprise RAG Platform. It provides a local corpus, query flow, retrieval configuration, trace inspection, evaluation views, and an interactive architecture map. The demonstration runs with deterministic fixtures and does not require a .NET API, LLM endpoint, API key, or Docker service.

## Run locally

Requirements: Node.js 24 and npm. The application and test tooling are verified on Node.js 24.

```bash
cd src/Rag.Playground
npm ci
npm run dev
```

Open the local URL printed by Next.js. The app has no environment variables or secrets to configure.

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

For Vercel, set the project root directory to `src/Rag.Playground`; the build and install commands use the app's `package.json` there.

Before running browser tests, install their browser once with `npx playwright install chromium`. Tests start a local server on port 3100 when one is not already running. Session history stores the latest 20 successful runs in session storage; the theme preference persists in local storage.

## Demo data and measurements

Corpus documents, chunks, and query runs are deterministic local fixtures. Demo response timing describes the browser-side simulation and does not represent production API, embedding, database, or model latency. Displayed published evaluation results are copied from the repository's generated deterministic benchmark report; they are not recomputed from the Playground corpus or from the current query settings. The benchmark measures retrieval and citation behavior against its own golden dataset. Its deterministic provider does not assess the quality of answers from a production language model.

The interface does not make external model calls. Its reranking, context budget, neighbor expansion, citation validation, query trace, and stage timing are local demonstrations; they do not describe API capabilities. To connect a live deployment, replace the local `RagGateway` implementation with an adapter that calls the platform API. `POST /query` accepts a question, `topK`, and an optional source/origin/document/file-type filter, then returns answer text and citation records. It does not return candidate/context chunks, a confidence score, or pipeline trace stages. Ingestion is available through `POST /documents`, with job status at `GET /jobs/{id}`. API-key authentication is opt-in through `RAG_API_KEY` or `Api:ApiKey`; without that setting, the API accepts unauthenticated requests. Configure any API key and HTTP LLM credentials on the server. Do not expose credentials in browser code.

## Main areas

- `app/` contains Next.js routes and application-wide styling.
- `features/` contains query, evaluation, architecture, and About views.
- `lib/contracts/` defines the gateway and platform data contracts.
- `lib/demo/corpus.ts` builds the stable, local demonstration corpora.
- `lib/evaluation/fixtures/` contains the checked-in deterministic evaluation data.
- `components/` contains shared layout and UI primitives.
