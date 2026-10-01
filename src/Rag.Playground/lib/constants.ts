import type { RetrievalConfig } from "@/lib/contracts";

export const DEFAULT_CONFIG: RetrievalConfig = {
    strategy: "recursive",
    topK: 5,
    mode: "vector",
    reranker: true,
    minRelevance: 0.7,
    neighbors: false,
    chunkSize: 800,
    chunkOverlap: 120,
    embeddingDimensions: 1536,
    maxContextTokens: 4096,
};

export const DEFAULT_QUESTION =
    "What is the company's refund policy and what exceptions apply?";
export const STRATEGY_LABELS: Record<string, string> = {
    fixed: "Fixed",
    recursive: "Recursive",
    "markdown-aware": "Markdown-aware",
    semantic: "Semantic",
};
export const STRATEGY_COLORS: Record<string, string> = {
    fixed: "#8b8fa3",
    recursive: "#6465e9",
    "markdown-aware": "#14a88a",
    semantic: "#dc9850",
};
