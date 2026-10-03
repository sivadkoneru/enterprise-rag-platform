# Rag.Playground

Next.js UI for an inspectable RAG reference platform. The public mode is a browser simulation;
private live mode connects through a server-only proxy to Rag.Api. Neither mode is tenant authorization.

## Clean checkout

Use Node 24 (the `.nvmrc` selects that major) and the committed npm lockfile:

```bash
npm ci
npm run fixtures:check
npm run lint
npm run typecheck
npm test
npm run build
npx playwright install --with-deps chromium
npm run test:e2e
npm start
```

Run these commands from `src/Rag.Playground`. The browser suite starts the standalone production
server on `127.0.0.1:3100`. `npm start` stages public/static assets into standalone output; it does
not run a development server. `npm run dev` remains available for editing.

Default `RAG_PLAYGROUND_MODE=demo` disables all `/api/client/*` routes before configuration is read.
For private live setup use [Compose instructions](../../docs/client-deployment.md). Credentials stay
operator-managed on the server. Public Vercel deployment must contain no backend credentials;
see [public deployment](../../docs/public-demo.md).

## Responsibilities

- `app`: routing and the private server proxy.
- `features/playground`: demo query state hook, answer/evidence and trace presentation.
- `features/evaluation`: deterministic artifact explorer, charts and case-detail drawer.
- `features/retrieval`: ranking inspection and lexical highlighting.
- `features/architecture`: diagram data, pure layout helpers, node and detail components.
- `features/integrations`: template setup controller and focused setup panels.
- `features/live`: API-backed screens; no fixture fallback on errors.
- `lib/demo`: deterministic simulation and session persistence.
- `lib/live`: API contracts, boundary validation, bounded SSE parser and transport.
- `lib/evaluation/fixtures`: generated copies of authoritative repository artifacts.

After regenerating benchmark artifacts or editing root Compose, run `npm run fixtures:write`.
CI checks equality. Marketing captures and visual regression baselines have separate purposes.

The simulation prepares evidence sentences and illustrative scores. It does not run Elasticsearch,
embeddings, reranking, or generation. Stage durations are scripted. Live stage timings are measured;
stage events are not model-token streaming. Reference resolution is not semantic citation support.
