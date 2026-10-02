import type { ChunkingStrategy, TraceStage } from "@/lib/contracts";

export interface EnvironmentCapabilities {
    reranker: boolean;
    hybrid: boolean;
    embeddingModel: string;
    embeddingDimensions: number;
    documentStore: string;
    vectorStore: string;
    jobStore: string;
    workbenchJobStore?: string;
    defaultQuery?: Pick<LiveQueryRequest, "topK" | "mode" | "reranker" | "minRelevance" | "neighbors" | "maxContextTokens">;
}
export interface IntegrationStatus {
    name: string;
    status: "not-configured" | "configured" | "healthy" | "failed";
    message?: string;
}
export interface ClientCorpus {
    id: string;
    name: string;
    description: string;
}
export interface IndexProfile {
    id: string;
    corpusId: string;
    name: string;
    strategy: ChunkingStrategy;
    chunkSize: number;
    chunkOverlap: number;
    embeddingModel: string;
    embeddingDimensions: number;
    status: string;
    documentCount: number;
    chunkCount: number;
    createdAt: string;
}
export interface ClientDocument {
    id: string;
    filename: string;
    content: string;
    chunkCount: number;
}
export interface PageResult<T> { items: T[]; total: number; offset: number; limit: number }
export interface ClientJob {
    id: string;
    kind: string;
    status: string;
    profileId?: string;
    completed: number;
    total: number;
    error?: string;
}
export interface LiveQueryRequest {
    question: string;
    corpusId: string;
    profileId: string;
    topK: number;
    mode: "vector" | "hybrid";
    reranker: boolean;
    minRelevance: number;
    neighbors: boolean;
    maxContextTokens: number;
}
export interface LiveChunk {
    id: string;
    documentId: string;
    filename: string;
    index: number;
    content: string;
    vectorScore: number | null;
    lexicalScore: number | null;
    fusionScore: number | null;
    rerankerScore: number | null;
    rankBefore: number;
    rankAfter: number;
    inContext: boolean;
    exclusionReason?: string;
    isNeighbor?: boolean;
}
export interface LiveQueryRun {
    id: string;
    createdAt: string;
    question: string;
    corpusId: string;
    profileId: string;
    answer: string;
    citations: { number: number; chunkId: string; documentId: string; valid: boolean }[];
    candidates: LiveChunk[];
    context: LiveChunk[];
    trace: TraceStage[];
    totalLatencyMs: number;
    contextTokens: number;
    outputTokens: number;
    promptTokens?: number | null;
    totalTokens?: number | null;
    tokenUsageKind: string;
    abstained: boolean;
    invalidCitations: string[];
}
export interface EvaluationQuestion {
    id: string;
    question: string;
    expectedSourceFile?: string;
    goldAnchors: { phrase: string; section?: string }[];
    expectedAnswer?: string;
    expectedAbstention?: boolean;
    answerKeywords?: string[];
    type?: string;
    difficulty?: string;
}
export interface EvaluationDatasetInput {
    profileIds: string[];
    questions: EvaluationQuestion[];
    topK?: number;
    mode?: "vector" | "hybrid";
    reranker?: boolean;
    minRelevance?: number;
    neighbors?: boolean;
    maxContextTokens?: number;
}
export interface LiveEvaluationMetrics {
    recallAt1: number | null;
    recallAt5: number | null;
    mrrAt5: number | null;
    citationAccuracy: number | null;
    citationPrecision: number | null;
    groundedness: number | null;
    abstentionAccuracy: number | null;
}
export interface EvaluationRun {
    id: string;
    status: string;
    questions?: EvaluationQuestion[];
    profiles: {
        profileId: string;
        profileName: string;
        metrics: LiveEvaluationMetrics;
        embeddingOperations: number;
        averageContextTokens: number;
        averageLatencyMs: number;
        outcomes: {
            questionId: string;
            question: string;
            expectedAnswer?: string;
            expectedSourceFile?: string;
            expectedAbstention: boolean;
            metrics: LiveEvaluationMetrics;
            run: LiveQueryRun;
        }[];
    }[];
}
