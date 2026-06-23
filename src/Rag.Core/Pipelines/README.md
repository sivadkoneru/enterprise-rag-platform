# Rag.Core Pipelines

Ingestion and query pipeline orchestration.

The ingestion pipeline should parse, chunk, embed, and persist chunks and vectors. The query pipeline should embed the question, retrieve vectors, hydrate chunks, build a grounded prompt, call chat, and return citations.

