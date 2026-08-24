---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.sln"
---

# .NET Project

- Target framework: net10.0.
- Use xUnit for tests.
- No mocking library is referenced in this repo; prefer hand-written fakes/in-memory test doubles over adding one.
- Use Microsoft.Extensions.Logging for application logging.
- Follow existing project dependency and package conventions.
- Do not add NuGet packages without a concrete requirement.
