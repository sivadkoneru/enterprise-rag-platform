# Rag.Core LLM

Embedding and chat client abstractions plus provider adapters.

Embedding and chat endpoints are configured separately through the same OpenAI-compatible option shape for public OpenAI and Azure OpenAI. `LLM_PROVIDER` selects the HTTP LLM adapter without changing pipeline code or configuration key names.

`LLM_SYSTEM_PROMPT` configures the grounding instruction sent with every chat request. The default prompt should require answers to use only supplied context, say when the answer is not present, and cite sources. Deterministic local chat behavior should honor the same no-answer contract so local tests match production grounding expectations.
