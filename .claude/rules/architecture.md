---
paths:
  - "src/**/*.cs"
  - "src/**/*.ts"
  - "src/**/*.tsx"
---

# Architecture

- Keep business logic in the application/domain layer.
- Keep infrastructure concerns behind explicit boundaries.
- Do not introduce new architectural patterns without a concrete need.
- Follow the existing dependency direction.
- Reuse existing application services before creating new ones.
- Do not bypass established abstractions without justification.