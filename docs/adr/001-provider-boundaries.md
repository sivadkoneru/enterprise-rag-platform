# ADR 001: Provider packages and keyed DI

Status: accepted for the single-operator reference release.

## Decision

Core exposes contracts without cloud SDK references. Opt-in provider packages register keyed services; missing packages fail explicitly rather than falling back to memory. Elasticsearch remains raw HTTP in Core to avoid a client SDK dependency.

## Consequences and alternatives

This keeps minimal consumers small and provider selection inspectable, at the cost of maintaining Elasticsearch request/response contracts and integration tests. A dedicated provider package becomes appropriate if that adapter grows substantially.
