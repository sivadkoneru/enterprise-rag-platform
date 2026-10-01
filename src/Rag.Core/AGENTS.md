# Rag.Core

- Do not add AWS, Azure, Cosmos, or MongoDB SDK dependencies to `Rag.Core`.
- Put new provider backends in `Rag.Providers.*` with an explicit `AddRag*` registration.
- Missing selected providers must throw an `InvalidOperationException` naming the package; never
  fall back to in-memory.

See [README.md](README.md) for the package split.
