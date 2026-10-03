# Public demo deployment

The public release is a simulation. Use one Vercel project with root `src/Rag.Playground`, Node 24,
install `npm ci`, and build `npm run build`. Set `RAG_PLAYGROUND_MODE=demo`. Supply **no** backend URL,
API key, model key, cloud credential, or private documents. The mode gate rejects every client proxy
request before reading backend configuration. A forged browser preference cannot enable it.

Require the named CI jobs before publishing main. Enable PR previews in the connected Vercel
project, verify the public-boundary browser test on the preview, and promote that exact deployment.
Record deployment URL, commit and date. Retain the previous deployment for rollback. Deployment
and branch-protection settings are external controls and must be verified in the actual project.

Private live mode uses a fixed operator destination, a shared backend API key, explicit origin/Host
validation, route allowlists, redirect rejection, bounded body reads, deadlines and API workload
limits. These controls do not authenticate a browser visitor. The API grants the proxy's authority
to whoever can reach an enabled proxy. Never deploy private-live mode anonymously on the internet.

If public live access is added later: put OIDC authentication before the proxy; validate identity
and authorization in the API; derive tenant scope server-side; enforce document ACLs before retrieval;
protect cookie-authenticated mutations against CSRF; add distributed quotas and provider spending
limits. Start with curated read-only corpora and reserve ingestion/evaluation for operators.

Destination policy follows [OWASP SSRF guidance](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html).
Private container addresses are intentional. Origins are defense in depth under
[OWASP CSRF guidance](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html).
