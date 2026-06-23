# Source Modules

Contains the .NET projects for the enterprise RAG platform.

- `Rag.Core`: shared abstractions, models, adapters, strategies, pipelines, and dependency injection.
- `Rag.Api`: ASP.NET Core HTTP surface.
- `Rag.Cli`: command-line surface.

All projects should use explicit package versions and should keep provider-specific dependencies isolated to adapter modules.
