# Local client deployment

The client profile runs the .NET API and standalone Next.js Playground with Docker Compose. The default `local-storage` Compose profile also starts MongoDB and Elasticsearch; their data stays in named volumes and the services are reachable only on the private Compose network. The Playground is published on `127.0.0.1:3000` by default. The API uses deterministic model responses and needs no LLM key for a first run.

## Start the stack

Use Docker Compose v2 and generate a unique API key before starting:

```bash
cp .env.client.example .env.client
openssl rand -hex 32
```

Put the generated value in `.env.client` as `RAG_API_KEY`, then build and start:

```bash
docker compose -f compose.client.yml --env-file .env.client up --build -d
```

Open <http://localhost:3000>, switch to **Client Environment**, and choose **Check status** on Integration setup. The readiness panel checks the API configuration and connected services. Use **Your data, end to end** on that page to create a corpus and ingestion profile, enqueue a source, and inspect documents. For local files, put supported `.txt`, `.md`, or `.pdf` files under `client-documents/` and ingest a path such as `/documents/handbook.md`; the API confines local access to that mounted read-only directory.

To use another host port, set `PLAYGROUND_PORT` in `.env.client`. Stop the stack with:

```bash
docker compose -f compose.client.yml --env-file .env.client down
```

The MongoDB and Elasticsearch volumes remain after `down`. Remove data only when you intend to delete it:

```bash
docker compose -f compose.client.yml --env-file .env.client down --volumes
```

## Configure providers

Integration setup downloads `generated.env.client` and `appsettings.Client.json` from the shared configuration catalog. The browser never accepts secret values; secret entries are blank in downloaded files. Copy the generated values into `.env.client` (or rename the download to `.env.client` for a new installation), then add credentials locally on the deployment host. Compose reads `.env.client`, not the downloaded filename. Keep both files out of source control.

To use an OpenAI-compatible provider, set `LLM_PROVIDER`, `LLM_API_KEY`, the embedding endpoint/model/dimensions, and chat endpoint/model. Keep `ELASTICSEARCH_VECTOR_DIMENSIONS` aligned with `LLM_EMBEDDING_DIMENSIONS`. Re-ingest into a new profile/index after changing embedding model or dimensions. The default Compose wiring uses MongoDB and Elasticsearch. Choose external stores in Integration setup to clear `COMPOSE_PROFILES`, which leaves local storage services stopped; set their provider and connection/endpoint variables in the environment file. The setup page reflects those choices in the generated files.

S3 and Azure Blob adapters are available when configured on the API. LocalStack and Azurite are optional Compose services:

```bash
# S3 emulator; the test credentials are only for LocalStack.
docker compose -f compose.client.yml --env-file .env.client --profile s3-emulator up -d localstack
# Set S3_ENDPOINT=http://localstack:4566, AWS_ACCESS_KEY_ID=test, and
# AWS_SECRET_ACCESS_KEY=test in .env.client before starting the API.

# Azure Blob emulator.
docker compose -f compose.client.yml --env-file .env.client --profile azure-emulator up -d azurite
# Set AZURE_BLOB_CONNECTION_STRING=UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://azurite
# in .env.client before starting the API.
```

For cloud endpoints, set provider endpoint variables and provide credentials only through the server environment. Restrict `LOCAL_SOURCE_ALLOWED_ROOTS` and `CLOUD_SOURCE_ALLOWED_PREFIXES` to the paths the API should accept. The host directory mounted read-only as `/documents` is selected by `CLIENT_DOCUMENTS_PATH`.

## Configuration and API behavior

`src/Rag.Playground/lib/integrations/config-catalog.json` is the catalog shared with the API build. It records option section/property bindings, environment aliases, secret redaction, validation guidance, provider applicability, and restart/reindex effects. Compose applies `.env.client` to the API container. `appsettings.Client.json` is an alternative .NET configuration file; place it beside the API's `appsettings.json` and set `ASPNETCORE_ENVIRONMENT=Client` for the API host to load it. The runtime panel makes read-only requests through same-origin `/api/client/*` routes; the browser does not receive `RAG_API_KEY` or call the API container directly. API runtime configuration is operator-managed and the catalog does not write to a running server.

The API key is optional for the platform generally, but this Compose profile requires a non-empty key and uses the same value for API authentication and the server-side Playground proxy. Health checks and Swagger are public API endpoints; other API routes use `X-API-Key`. Use HTTPS and a managed secret store before exposing a deployment beyond a trusted local machine.

## Build and deployment

`src/Rag.Api/Dockerfile.client` publishes with the .NET 10 SDK and runs the ASP.NET runtime as the non-root `app` user. `src/Rag.Playground/Dockerfile.client` builds a Next standalone output with Node 24 and runs as a non-root user. Vercel deployments should set the project root directory to `src/Rag.Playground`; the local Compose files are not needed there.

For the demo-only web app, Node 24 and npm are sufficient:

```bash
cd src/Rag.Playground
npm ci
npm run dev
```
