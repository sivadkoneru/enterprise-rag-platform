# Tests

- Test observable behavior; do not use reflection or source-text assertions.
- Mark Docker-dependent tests with `[Trait("Category", "Integration")]` and use
  `DockerPrerequisite.StartAsync`.
- Cloud-source suites must join `CloudSourceCollection` because they share temporary storage.

See [README.md](README.md) for the coverage map.
