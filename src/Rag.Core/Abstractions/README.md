# Rag.Core Abstractions

Shared interfaces and contracts for parsers, document sources, chunkers, LLM clients, stores, vector search, ingestion jobs, and pipelines.

Keep contracts provider-neutral and stable. Adapter-specific types should not leak through these interfaces.

Document source contracts resolve `file`, `s3`, and `azureblob` URIs into source items that parser adapters can consume as paths. `IDocumentParser` handles one-document files; `IMultiDocumentParser` handles structured files such as JSONL and CSV where one file can produce many documents. Vector search contracts accept optional metadata filters for source URI, origin, document id, and file type so query orchestration does not need backend-specific logic.
