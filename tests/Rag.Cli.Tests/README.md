# Rag.Cli.Tests

Unit tests for `Rag.Cli`.

## Purpose

Drive `CliApplication.RunAsync` directly (bypassing the process entry point) to cover exit-code
propagation and cooperative cancellation:

- A failing command (for example, ingesting a nonexistent path) returns a non-zero exit code.
- A succeeding command returns exit code `0`.
- An unrecognized `--option` in the `ingest` argument list is rejected by the argument validator.
- Cancelling the token passed to `RunAsync` unwinds the run with a non-zero exit code and without
  letting an `OperationCanceledException` escape.

## Dependencies

Expected test dependencies include xUnit and FluentAssertions. Tests build a real DI container via
`AddRagPlatform` (memory stores, deterministic LLM client) so they can run without network access or
containerized services.
