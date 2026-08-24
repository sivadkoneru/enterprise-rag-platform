# Rag.Core

`Rag.Core` must stay free of provider SDKs. Never add an AWS, Azure, Cosmos, or MongoDB
`PackageReference` to `Rag.Core.csproj` — the current reference set (Extensions abstractions,
`Markdig`, `PdfPig`) is deliberate, and its comments explain the omissions.

- The Elasticsearch vector store lives here only because it talks raw HTTP with no provider SDK.
- New backends go in a `Rag.Providers.*` package that owns its own SDK dependency and exposes one
  `AddRag*(configuration)` extension registering keyed services.
- Selecting a `DOC_STORE`, `JOB_STORE`, or source scheme whose package was never referenced must
  throw `InvalidOperationException` naming the missing package — never silently fall back to the
  in-memory default.

See [../README.md](../README.md) for the rationale and the per-package split.
