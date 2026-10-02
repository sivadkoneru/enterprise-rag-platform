import type { EvaluationQuestion } from "./contracts";

function isRecord(value: unknown): value is Record<string, unknown> {
    return !!value && typeof value === "object" && !Array.isArray(value);
}

export function parseEvaluationQuestions(text: string): EvaluationQuestion[] {
    const parsed: unknown = JSON.parse(text);
    const questions = Array.isArray(parsed) ? parsed : parsed && typeof parsed === "object" && "questions" in parsed ? parsed.questions : null;
    if (!Array.isArray(questions) || !questions.length || questions.length > 500) throw new Error("Provide between 1 and 500 evaluation questions.");
    const ids = new Set<string>();
    return questions.map((value: unknown, index) => {
        if (!isRecord(value) || typeof value.id !== "string" || !value.id.trim() || typeof value.question !== "string" || !value.question.trim()) throw new Error(`Question ${index + 1} needs a unique id and question text.`);
        if (ids.has(value.id)) throw new Error(`Duplicate question id: ${value.id}`);
        ids.add(value.id);
        const anchors = "goldAnchors" in value ? value.goldAnchors : [];
        if (!Array.isArray(anchors) || anchors.some(anchor => !anchor || typeof anchor !== "object" || !("phrase" in anchor) || typeof anchor.phrase !== "string" || !anchor.phrase.trim())) throw new Error(`Question ${value.id} needs valid evidence phrases in goldAnchors.`);
        const abstention = "expectedAbstention" in value && value.expectedAbstention === true;
        if (!abstention && !anchors.length) throw new Error(`Question ${value.id} needs evidence anchors or expectedAbstention: true.`);
        if (!abstention && (!("expectedSourceFile" in value) || typeof value.expectedSourceFile !== "string" || !value.expectedSourceFile.trim())) throw new Error(`Question ${value.id} needs an expectedSourceFile to disambiguate evidence.`);
        for (const key of ["expectedSourceFile", "expectedAnswer", "difficulty", "type"] as const) {
            if (key in value && typeof value[key] !== "string") throw new Error(`Question ${value.id}: ${key} must be a string.`);
        }
        if ("expectedAbstention" in value && typeof value.expectedAbstention !== "boolean") throw new Error(`Question ${value.id}: expectedAbstention must be true or false.`);
        if ("answerKeywords" in value && (!Array.isArray(value.answerKeywords) || value.answerKeywords.some(k => typeof k !== "string"))) throw new Error(`Question ${value.id}: answerKeywords must be strings.`);
        return { ...value, goldAnchors: anchors } as EvaluationQuestion;
    });
}
export const evaluationTemplate = {
    questions: [
        { id: "policy-001", question: "What is the refund window?", difficulty: "easy", expectedSourceFile: "policy.md", goldAnchors: [{ phrase: "Refunds are available within 30 days", section: "Refund policy" }], expectedAnswer: "Refunds are available within 30 days.", expectedAbstention: false },
        { id: "unknown-001", question: "What is the policy for an unsupported topic?", difficulty: "hard", goldAnchors: [], expectedAbstention: true },
    ],
};
