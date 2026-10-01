export type ChunkingStrategy =
    "fixed" | "recursive" | "markdown-aware" | "semantic";
export type CorpusId = "handbook" | "product" | "support";
export type DataOrigin = "published-benchmark" | "demo-fixture";
export type StageStatus =
    "queued" | "running" | "complete" | "skipped" | "failed";

export interface Corpus {
    id: CorpusId;
    name: string;
    description: string;
    documentCount: number;
    chunkCount: number;
}

export interface CorpusDocument {
    id: string;
    corpusId: CorpusId;
    filename: string;
    title: string;
    format: "md" | "txt" | "pdf";
    content: string;
    chunkCount: number;
    sections: string[];
}

export interface DemoChunk {
    id: string;
    documentId: string;
    filename: string;
    section: string;
    index: number;
    content: string;
    summary: string;
    concepts: string[];
    topic: "refund" | "security" | "support" | "api" | "onboarding" | "general";
}

export interface RetrievalConfig {
    strategy: ChunkingStrategy;
    topK: number;
    mode: "vector" | "hybrid";
    reranker: boolean;
    minRelevance: number;
    neighbors: boolean;
    chunkSize: number;
    chunkOverlap: number;
    embeddingDimensions: number;
    maxContextTokens: number;
}

export interface QueryRequest {
    corpusId: CorpusId;
    question: string;
    config: RetrievalConfig;
    simulateFailure?: boolean;
}

export interface RetrievedChunk {
    id: string;
    documentId: string;
    filename: string;
    section: string;
    index: number;
    content: string;
    tokens: number;
    vectorScore: number;
    lexicalScore: number;
    retrievalScore: number;
    rerankerScore: number | null;
    score: number;
    rankBefore: number;
    rankAfter: number;
    inContext: boolean;
    exclusionReason?: string;
    isNeighbor?: boolean;
    concepts: string[];
}

export interface Citation {
    number: number;
    chunkId: string;
    documentId: string;
}

export interface AnswerSegment {
    text: string;
    citationNumber?: number;
}

export interface TraceStage {
    id: string;
    name: string;
    status: StageStatus;
    durationMs: number;
    detail: string;
    diagnostics: Record<string, string | number | boolean>;
}

export interface QueryRun {
    id: string;
    createdAt: string;
    request: QueryRequest;
    answer: AnswerSegment[];
    citations: Citation[];
    candidates: RetrievedChunk[];
    context: RetrievedChunk[];
    trace: TraceStage[];
    totalLatencyMs: number;
    contextTokens: number;
    outputTokens: number;
    confidence: "High" | "Partial" | "Insufficient";
    confidenceReason: string;
    abstained: boolean;
    provenance: DataOrigin;
}

export interface PipelineEvent {
    stages: TraceStage[];
}

export interface EvaluationCase {
    id: string;
    question: string;
    type: string;
    difficulty: string;
    expectedSourceFile: string | null;
    goldAnchors: { phrase: string; section: string }[];
    answerKeywords: string[];
    absentTerms: string[];
    notes: string;
}

export interface EvaluationOutcome {
    questionId: string;
    type: string;
    difficulty: string;
    retrievedChunks: string[];
    recallAt1: number;
    recallAt5: number;
    reciprocalRank: number;
    topCitationCorrect: boolean;
    citationPrecision: number;
    groundedness: number;
    supported: boolean;
    supportZScore: number;
    fullyCovered: boolean;
    contextChars: number;
}

export interface StrategyBenchmark {
    strategy: string;
    recall1: number;
    recall5: number;
    mrr5: number;
    citationAccuracy1: number;
    citationPrecision5: number;
    groundedness: number;
    abstentionAccuracy: number;
    indexEmbedCalls: number;
    averageContextTokens: number;
    chunkCount: number;
    falseSupportRate: number;
    outcomes: EvaluationOutcome[];
}

export interface EvaluationDataset {
    questionCount: number;
    strategies: StrategyBenchmark[];
    questions: EvaluationCase[];
}

export interface ArchitectureNode {
    id: string;
    name: string;
    responsibility: string;
    technology: string;
    inputs: string;
    outputs: string;
    considerations: string[];
    implementation: "Implemented backend" | "Simulated in demo";
}

export interface RagGateway {
    listCorpora(): Promise<Corpus[]>;
    listDocuments(corpusId: CorpusId): Promise<CorpusDocument[]>;
    getDocument(documentId: string): Promise<CorpusDocument>;
    runQuery(
        request: QueryRequest,
        options: {
            signal: AbortSignal;
            onEvent: (event: PipelineEvent) => void;
        },
    ): Promise<QueryRun>;
    getEvaluation(): Promise<EvaluationDataset>;
}
