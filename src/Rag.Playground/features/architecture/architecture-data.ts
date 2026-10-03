import type { ArchitectureNode } from "@/lib/contracts";

export type ArchitectureGroup =
    "ingestion" | "query" | "operations" | "quality";

export interface DiagramNode extends ArchitectureNode {
    x: number;
    y: number;
    group: ArchitectureGroup;
}

export interface ArchitectureEdge {
    from: string;
    to: string;
    label: string;
    kind?: "supporting";
}

export const diagramSize = { width: 1250, height: 740 };

export const architectureNodes: DiagramNode[] = [
    {
        id: "sources",
        name: "Document sources",
        x: 110,
        y: 130,
        group: "ingestion",
        responsibility:
            "Enumerates local paths and cloud object prefixes through registered source adapters.",
        technology:
            "File source in Rag.Core; opt-in S3 and Azure Blob source providers.",
        inputs: "Local paths, s3:// prefixes, or azureblob:// prefixes.",
        outputs: "Source items for parsing.",
        considerations: [
            "S3 and Azure Blob adapters live in separate provider packages.",
            "A cloud scheme requires the corresponding provider registration.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "jobs",
        name: "Async ingestion jobs",
        x: 110,
        y: 300,
        group: "operations",
        responsibility:
            "Queues ingestion work and exposes progress, status, and cooperative job controls.",
        technology:
            "IIngestionJobQueue, background worker, and memory or MongoDB job store.",
        inputs: "Source URIs, ingestion options, and job controls.",
        outputs: "Job state and document/chunk progress counts.",
        considerations: [
            "MongoDB job persistence is selected through configuration.",
            "Pause and cancel take effect between source items.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "parsing",
        name: "Parse & normalize",
        x: 320,
        y: 130,
        group: "ingestion",
        responsibility:
            "Selects a parser for each source item and normalizes extracted text.",
        technology: "IDocumentParser adapters registered in Rag.Core.",
        inputs: "Materialized source item and file metadata.",
        outputs: "Parsed document text and metadata.",
        considerations: [
            "Parser resolution uses file extension and content type.",
            "PDF and Markdown parsing are behind core parser adapters.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "chunking",
        name: "Chunking strategy",
        x: 530,
        y: 130,
        group: "ingestion",
        responsibility:
            "Splits normalized text into passages that retain document and section references.",
        technology:
            "Fixed, recursive, Markdown-aware, and semantic IChunkingStrategy implementations.",
        inputs: "Parsed document plus configured strategy and size options.",
        outputs: "Text chunks with stable identifiers and metadata.",
        considerations: [
            "Strategy choice affects boundaries, retrieval behavior, and embedding work.",
            "Chunk previews are available in the CLI and API.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "embeddings",
        name: "Embedding client",
        x: 740,
        y: 130,
        group: "ingestion",
        responsibility:
            "Embeds document chunks during ingestion and question text during a query.",
        technology:
            "IEmbeddingClient with deterministic local and OpenAI-compatible HTTP clients.",
        inputs: "Text and configured embedding model.",
        outputs: "Vectors with the configured dimensions.",
        considerations: [
            "Index dimensions must match the selected embedding model.",
            "Embedding and chat clients have separate endpoint configuration.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "vector-store",
        name: "Elasticsearch vector store",
        x: 950,
        y: 130,
        group: "ingestion",
        responsibility:
            "Indexes chunk vectors and searches for the nearest candidates for a question.",
        technology:
            "IVectorStore adapters: in-memory and Elasticsearch HTTP integration in Rag.Core.",
        inputs: "Chunk identifiers, vectors, and searchable metadata.",
        outputs: "Ordered vector matches and scores.",
        considerations: [
            "Elasticsearch uses raw HTTP and adds no provider SDK dependency to Rag.Core.",
            "Source, origin, document, and file-type filters are supported.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "metadata-store",
        name: "Document & metadata store",
        x: 530,
        y: 300,
        group: "ingestion",
        responsibility:
            "Stores source document text and chunk metadata so retrieved matches can be hydrated.",
        technology:
            "IDocumentStore adapters: memory, file, MongoDB, and Cosmos DB.",
        inputs: "Parsed documents and their chunks.",
        outputs: "Stored text and metadata resolved by chunk identifier.",
        considerations: [
            "MongoDB and Cosmos DB are opt-in provider packages.",
            "The query pipeline restores match order after loading chunks.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "query-request",
        name: "Query API",
        x: 110,
        y: 500,
        group: "query",
        responsibility:
            "Accepts a question, top-K limit, and optional source filters for the query pipeline.",
        technology: "ASP.NET Core POST /query and the CLI query command.",
        inputs: "Question, topK, and optional source/origin/document/file-type filter.",
        outputs: "A query pipeline request.",
        considerations: [
            "The API does not expose the Playground's strategy, reranker, neighbor, or context-budget controls.",
            "API-key authentication is opt-in through RAG_API_KEY or Api:ApiKey; without a key, requests are unauthenticated.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "retrieval",
        name: "Vector retrieval & hydration",
        x: 320,
        y: 500,
        group: "query",
        responsibility:
            "Embeds the question, searches the vector store, then loads matching chunks from the document store.",
        technology:
            "IQueryPipeline with IEmbeddingClient, IVectorStore, and IDocumentStore.",
        inputs: "Question vector, topK, and optional vector search filter.",
        outputs: "Ordered retrieved chunks and citation records.",
        considerations: [
            "Core query retrieval uses vector search; it has no lexical hybrid stage.",
            "Missing stored chunks are omitted from both prompt context and citations.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "reranker",
        name: "Reranker",
        x: 530,
        y: 500,
        group: "query",
        responsibility:
            "Reorders candidates using the demo's illustrative relevance scores.",
        technology:
            "Local deterministic Playground ranking logic; no backend reranker contract is currently wired.",
        inputs: "Retrieved demo candidates and the question topic.",
        outputs: "Demo candidate ordering and illustrative scores.",
        considerations: [
            "This stage is simulated in the Playground.",
            "It does not call a cross-encoder or change the .NET API query behavior.",
        ],
        implementation: "Simulated in demo",
    },
    {
        id: "context-builder",
        name: "Context builder",
        x: 740,
        y: 500,
        group: "query",
        responsibility:
            "Applies demo-only context-budget and neighbor settings before showing selected evidence.",
        technology:
            "Playground fixture logic; the backend query pipeline instead formats its retrieved chunks into a prompt.",
        inputs: "Demo ranked chunks, context budget, and optional neighbor setting.",
        outputs: "Selected context passages for the demo answer.",
        considerations: [
            "Context budget and neighbor expansion shown here are simulated frontend controls.",
            "The current API does not accept these Playground settings.",
        ],
        implementation: "Simulated in demo",
    },
    {
        id: "llm",
        name: "Chat model",
        x: 950,
        y: 500,
        group: "query",
        responsibility:
            "Generates the answer from the question and retrieved evidence context.",
        technology:
            "IChatClient with deterministic local and OpenAI-compatible HTTP implementations.",
        inputs: "Grounding system prompt, question, and retrieved chunk context.",
        outputs: "Answer text.",
        considerations: [
            "Grounding instructions come from LLM_SYSTEM_PROMPT/options.",
            "The Playground uses a prepared local fixture response without external model calls.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "citation-validation",
        name: "Citation validation",
        x: 1160,
        y: 500,
        group: "query",
        responsibility:
            "Checks that demo answer references resolve to admitted evidence chunks.",
        technology:
            "Playground fixture validation; the current backend returns citation records without a separate validation stage.",
        inputs: "Demo answer references and selected chunk identifiers.",
        outputs: "Resolved citation records in the local result.",
        considerations: [
            "This validation stage is simulated in the Playground.",
            "The backend creates citations from vector matches that were successfully hydrated.",
        ],
        implementation: "Simulated in demo",
    },
    {
        id: "response",
        name: "API response",
        x: 1160,
        y: 650,
        group: "query",
        responsibility:
            "Returns generated answer text and its source citation records to the caller.",
        technology: "RagAnswer with Answer and Citations from POST /query.",
        inputs: "Chat completion text and citations derived from retrieved matches.",
        outputs: "JSON answer text and citation list.",
        considerations: [
            "Legacy /query returns answers and citations; /api/v1/queries streams measured stage events without a confidence probability.",
            "The Playground's richer response display is produced from local fixtures.",
        ],
        implementation: "Implemented backend",
    },
    {
        id: "observability",
        name: "Query diagnostics",
        x: 740,
        y: 650,
        group: "operations",
        responsibility:
            "Shows stage timings, scores, and diagnostic details generated for a Playground query.",
        technology:
            "Local demo trace fixtures; backend query endpoint does not emit a TraceStage sequence.",
        inputs: "Playground simulation state and fixture timing values.",
        outputs: "Illustrative query-stage timeline and diagnostics.",
        considerations: [
            "Displayed latency is not production API, database, embedding, or model latency.",
            "This is not a hosted telemetry or backend observability service.",
        ],
        implementation: "Simulated in demo",
    },
    {
        id: "evaluation",
        name: "Evaluation harness",
        x: 320,
        y: 650,
        group: "quality",
        responsibility:
            "Measures retrieval and citation behavior against a golden question dataset.",
        technology:
            "Rag.Evals deterministic scorers, generated reports, and regression-floor tests.",
        inputs: "Golden questions, expected evidence, and retrieval outcomes.",
        outputs: "Recall, MRR, source-reference diagnostics, lexical overlap, and harness abstention metrics.",
        considerations: [
            "The harness is separate from the query response path.",
            "Published scores use the deterministic provider and do not measure production answer quality.",
        ],
        implementation: "Implemented backend",
    },
];

export const architectureEdges: ArchitectureEdge[] = [
    { from: "jobs", to: "sources", label: "enumerate sources" },
    { from: "sources", to: "parsing", label: "source item" },
    { from: "parsing", to: "chunking", label: "normalized text" },
    { from: "chunking", to: "embeddings", label: "chunk text" },
    { from: "chunking", to: "metadata-store", label: "documents + chunks" },
    { from: "embeddings", to: "vector-store", label: "chunk vectors" },
    { from: "query-request", to: "embeddings", label: "question text" },
    { from: "query-request", to: "retrieval", label: "topK + filters" },
    { from: "embeddings", to: "retrieval", label: "question vector" },
    { from: "vector-store", to: "retrieval", label: "ranked matches" },
    { from: "retrieval", to: "reranker", label: "retrieved chunks" },
    { from: "reranker", to: "context-builder", label: "demo ordering" },
    { from: "metadata-store", to: "context-builder", label: "chunk text" },
    { from: "context-builder", to: "llm", label: "evidence prompt" },
    { from: "llm", to: "citation-validation", label: "demo answer" },
    {
        from: "citation-validation",
        to: "response",
        label: "answer + citations",
    },
    {
        from: "retrieval",
        to: "evaluation",
        label: "retrieval outcomes",
        kind: "supporting",
    },
    {
        from: "retrieval",
        to: "observability",
        label: "demo diagnostics",
        kind: "supporting",
    },
    {
        from: "response",
        to: "observability",
        label: "demo timing",
        kind: "supporting",
    },
];
