# Rag.Core Stores

Document store repository contracts and adapters.

Store adapters are selected by `DOC_STORE` and do not change pipeline logic. The current MongoDB and Cosmos DB adapter classes preserve the swap point while using local file-backed persistence in this scaffold.
