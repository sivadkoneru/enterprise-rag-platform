# Rag.Core Dependency Injection

Service registration extensions for the platform.

`AddRagPlatform(configuration)` should compose parser adapters, source adapters, chunking strategies, LLM clients, stores, vector search, ingestion job services, pipelines, options, and resilience policies.

Source providers are selected by URI scheme rather than a single provider selector: `file`, `s3`, and `azureblob` should all be registered together. `LLM_SYSTEM_PROMPT`, S3 endpoint/region settings, Azure Blob connection settings, ingestion parallelism, and vector filter behavior should all flow through options binding.
