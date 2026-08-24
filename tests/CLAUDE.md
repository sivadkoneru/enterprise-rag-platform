# Tests

Repo-specific conventions beyond `.claude/rules/test-csharp.md`:

- Assert observable behavior. Do not locate members by reflection or assert on substrings of
  repository files — the compiler already catches renames. When guarding a file against drift, test
  the property that matters: `Rag.Core.Tests/EnvExampleBindingTests.cs` probes every `.env.example`
  key through `AddRagPlatform` and fails if a key binds nothing.
- Docker-dependent tests carry `[Trait("Category", "Integration")]` and start containers through
  `Rag.Integration.Tests/DockerPrerequisite.StartAsync`, so a missing daemon yields one actionable
  message instead of a Testcontainers stack trace.
- New cloud-source suites join `[Collection(CloudSourceCollection.Name)]` — it disables
  parallelization because those suites share the `rag-ingest` temporary directory.

See [README.md](README.md) for the current coverage map.
