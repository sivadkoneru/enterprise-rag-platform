import type {
    Corpus,
    CorpusDocument,
    CorpusId,
    DemoChunk,
} from "@/lib/contracts";

type Topic = DemoChunk["topic"];

interface DocumentSeed {
    id: string;
    filename: string;
    title: string;
    topic: Topic;
    concepts: string[];
    summary: string;
    sections: string[];
    format?: CorpusDocument["format"];
}

const handbookSeeds: DocumentSeed[] = [
    {
        id: "handbook-refunds",
        filename: "handbook.md",
        title: "Refund Policy",
        topic: "refund",
        concepts: ["refund", "purchase", "30 days"],
        summary:
            "Eligible direct purchases receive a full refund when requested within 30 days.",
        sections: ["Eligibility", "Request a refund", "Processing"],
    },
    {
        id: "handbook-agreements",
        filename: "enterprise-agreements.md",
        title: "Enterprise Exceptions",
        topic: "refund",
        concepts: ["enterprise", "contract", "terms"],
        summary:
            "Negotiated contract terms take precedence over the standard refund policy.",
        sections: ["Contract precedence", "Review an agreement", "Escalation"],
    },
    {
        id: "handbook-services",
        filename: "handbook.md",
        title: "Non-refundable Services",
        topic: "refund",
        concepts: ["setup", "custom services", "refund"],
        summary:
            "Completed setup and custom services are excluded from standard refunds.",
        sections: ["Setup services", "Custom work", "Before purchase"],
    },
    {
        id: "handbook-refund-procedures",
        filename: "refund-procedures.md",
        title: "Refund Processing",
        topic: "refund",
        concepts: ["processing", "business days", "payment"],
        summary:
            "Approved refunds are generally returned to the original payment method in five to ten business days.",
        sections: ["Review timeline", "Payment methods", "Status"],
    },
    {
        id: "handbook-reseller",
        filename: "reseller-policy.md",
        title: "Reseller Purchases",
        topic: "refund",
        concepts: ["reseller", "partner", "60 days"],
        summary:
            "Reseller purchases follow partner-specific terms and do not change the policy for direct purchases.",
        sections: ["Partner terms", "Contact the reseller", "Direct purchases"],
    },
    {
        id: "handbook-security",
        filename: "security-handbook.md",
        title: "Account Security",
        topic: "security",
        concepts: ["security", "account", "authentication"],
        summary:
            "Use individual accounts and strong authentication to protect access to company workspaces.",
        sections: [
            "Account access",
            "Multi-factor authentication",
            "Review access",
        ],
    },
    {
        id: "handbook-data",
        filename: "data-protection.md",
        title: "Data Protection",
        topic: "security",
        concepts: ["data", "encryption", "retention"],
        summary:
            "Sensitive data should be classified, encrypted in transit, and retained only as long as needed.",
        sections: ["Classification", "Encryption", "Retention"],
    },
    {
        id: "handbook-incident",
        filename: "incident-response.md",
        title: "Incident Response",
        topic: "security",
        concepts: ["incident", "report", "response"],
        summary:
            "Report suspected security incidents promptly so the response team can contain and investigate them.",
        sections: ["Report an incident", "Containment", "Follow-up"],
    },
    {
        id: "handbook-access",
        filename: "access-control.md",
        title: "Access Control",
        topic: "security",
        concepts: ["least privilege", "permissions", "access"],
        summary:
            "Grant only the permissions needed for a person's current role and review them regularly.",
        sections: ["Role access", "Approval", "Quarterly review"],
    },
    {
        id: "handbook-passwords",
        filename: "identity-guide.md",
        title: "Identity and Credentials",
        topic: "security",
        concepts: ["credentials", "password", "identity"],
        summary:
            "Never share credentials; use the approved identity provider and report suspected compromise.",
        sections: [
            "Credential safety",
            "Single sign-on",
            "Compromised account",
        ],
    },
    {
        id: "handbook-vendors",
        filename: "vendor-security.md",
        title: "Vendor Security",
        topic: "security",
        concepts: ["vendor", "security review", "data"],
        summary:
            "A vendor handling company data must complete the security review before access is granted.",
        sections: ["Vendor review", "Data access", "Offboarding"],
    },
    {
        id: "handbook-privacy",
        filename: "privacy-guide.md",
        title: "Privacy Requests",
        topic: "security",
        concepts: ["privacy", "personal data", "request"],
        summary:
            "Route personal-data requests to the privacy team and preserve the request's audit trail.",
        sections: ["Intake", "Verification", "Response"],
    },
    {
        id: "handbook-backups",
        filename: "continuity-plan.md",
        title: "Backup and Recovery",
        topic: "security",
        concepts: ["backup", "recovery", "availability"],
        summary:
            "Backups must be protected, monitored, and tested against documented recovery objectives.",
        sections: ["Backup scope", "Restore test", "Recovery objectives"],
    },
    {
        id: "handbook-support",
        filename: "support-guide.md",
        title: "Contacting Support",
        topic: "support",
        concepts: ["support", "ticket", "response"],
        summary:
            "Include impact, steps to reproduce, and a request identifier when opening a support ticket.",
        sections: ["Open a ticket", "Priority", "Follow-up"],
    },
    {
        id: "handbook-escalation",
        filename: "support-escalation.md",
        title: "Support Escalation",
        topic: "support",
        concepts: ["escalation", "severity", "support"],
        summary:
            "Escalate a blocked production workflow with its severity, business impact, and incident link.",
        sections: ["When to escalate", "Provide context", "Resolution"],
    },
    {
        id: "handbook-status",
        filename: "service-status.md",
        title: "Service Status",
        topic: "support",
        concepts: ["status", "outage", "maintenance"],
        summary:
            "Check the service status page for confirmed incidents and planned maintenance updates.",
        sections: ["Status page", "Subscriptions", "Maintenance"],
    },
    {
        id: "handbook-billing-help",
        filename: "billing-support.md",
        title: "Billing Questions",
        topic: "support",
        concepts: ["billing", "invoice", "support"],
        summary:
            "Billing requests should include the invoice number and organization name, never payment credentials.",
        sections: ["Invoice questions", "Payment issue", "Account changes"],
    },
    {
        id: "handbook-troubleshooting",
        filename: "troubleshooting.md",
        title: "Troubleshooting Basics",
        topic: "support",
        concepts: ["troubleshooting", "logs", "reproduce"],
        summary:
            "Capture the exact error, time, and safe diagnostic details before changing a production setting.",
        sections: ["Gather evidence", "Reproduce", "Share diagnostics"],
    },
    {
        id: "handbook-feedback",
        filename: "feedback.md",
        title: "Product Feedback",
        topic: "support",
        concepts: ["feedback", "feature request", "support"],
        summary:
            "Describe the user problem and desired outcome when submitting product feedback.",
        sections: ["Submit feedback", "Feature requests", "Updates"],
    },
    {
        id: "handbook-availability",
        filename: "availability.md",
        title: "Availability Commitments",
        topic: "support",
        concepts: ["availability", "service level", "support"],
        summary:
            "Availability commitments are defined by the applicable service plan and signed agreement.",
        sections: ["Plan coverage", "Service credits", "Contact"],
    },
    {
        id: "handbook-api-auth",
        filename: "api-authentication.md",
        title: "API Authentication",
        topic: "api",
        concepts: ["api", "token", "authentication"],
        summary:
            "Send a scoped bearer token over TLS and rotate credentials according to the security policy.",
        sections: ["Create a token", "Send requests", "Rotate credentials"],
    },
    {
        id: "handbook-api-errors",
        filename: "api-errors.md",
        title: "API Errors",
        topic: "api",
        concepts: ["api", "error", "request id"],
        summary:
            "Use the response status and request identifier to diagnose API failures safely.",
        sections: ["Status codes", "Request identifiers", "Retries"],
    },
    {
        id: "handbook-api-pagination",
        filename: "api-pagination.md",
        title: "Pagination",
        topic: "api",
        concepts: ["api", "pagination", "cursor"],
        summary:
            "Follow the returned cursor to retrieve additional API results without skipping pages.",
        sections: ["List results", "Continue a page", "End of results"],
    },
    {
        id: "handbook-api-limits",
        filename: "api-limits.md",
        title: "Rate Limits",
        topic: "api",
        concepts: ["api", "rate limit", "retry"],
        summary:
            "Respect rate-limit response headers and use bounded backoff when retrying requests.",
        sections: ["Limit headers", "Retry timing", "Capacity"],
    },
    {
        id: "handbook-api-webhooks",
        filename: "webhooks.md",
        title: "Webhooks",
        topic: "api",
        concepts: ["api", "webhook", "signature"],
        summary:
            "Validate webhook signatures and make event processing safe to retry.",
        sections: ["Verify signatures", "Handle events", "Retries"],
    },
    {
        id: "handbook-api-versioning",
        filename: "api-versioning.md",
        title: "API Versions",
        topic: "api",
        concepts: ["api", "version", "compatibility"],
        summary:
            "Pin a supported API version and review deprecation notices before upgrading.",
        sections: ["Select a version", "Compatibility", "Upgrade"],
    },
    {
        id: "handbook-api-sources",
        filename: "source-connectors.md",
        title: "Source Connectors",
        topic: "api",
        concepts: ["api", "source", "connector"],
        summary:
            "Configure file, S3, or Azure Blob source URIs through the registered connector adapters.",
        sections: ["Local files", "S3", "Azure Blob"],
    },
    {
        id: "handbook-onboarding",
        filename: "getting-started.md",
        title: "Workspace Onboarding",
        topic: "onboarding",
        concepts: ["onboarding", "workspace", "setup"],
        summary:
            "New workspace members should confirm ownership, access, and support contacts during setup.",
        sections: ["Workspace setup", "Invite members", "First steps"],
    },
    {
        id: "handbook-ingestion",
        filename: "ingestion-guide.md",
        title: "Document Ingestion",
        topic: "onboarding",
        concepts: ["onboarding", "ingestion", "documents"],
        summary:
            "Supported documents are parsed, normalized, divided into chunks, and indexed for retrieval.",
        sections: ["Prepare files", "Start ingestion", "Check results"],
    },
    {
        id: "handbook-first-query",
        filename: "query-guide.md",
        title: "Run a First Query",
        topic: "onboarding",
        concepts: ["onboarding", "query", "citations"],
        summary:
            "Ask a focused question and inspect the cited source passages alongside the answer.",
        sections: ["Write a question", "Review evidence", "Refine"],
    },
    {
        id: "handbook-configuration",
        filename: "configuration-guide.md",
        title: "Configuration Basics",
        topic: "onboarding",
        concepts: ["onboarding", "configuration", "options"],
        summary:
            "Platform settings bind through options, with environment values taking precedence over JSON.",
        sections: ["Configuration sources", "Provider selection", "Validate"],
    },
    {
        id: "handbook-environments",
        filename: "environments.md",
        title: "Environments",
        topic: "onboarding",
        concepts: ["onboarding", "environment", "deployment"],
        summary:
            "Keep local, test, and production configuration separate and document each environment's providers.",
        sections: ["Local", "Test", "Production"],
    },
    {
        id: "handbook-platform",
        filename: "platform-overview.md",
        title: "Platform Overview",
        topic: "general",
        concepts: ["platform", "retrieval", "documents"],
        summary:
            "The platform connects document sources, parsing, storage, vector retrieval, and grounded answers.",
        sections: ["Components", "Data flow", "Boundaries"],
    },
    {
        id: "handbook-chunking",
        filename: "chunking.md",
        title: "Chunking Strategies",
        topic: "general",
        concepts: ["chunking", "strategy", "retrieval"],
        summary:
            "Chunking strategies trade off context continuity, retrieval precision, and embedding work.",
        sections: ["Fixed", "Recursive", "Markdown-aware"],
    },
    {
        id: "handbook-embeddings",
        filename: "embeddings.md",
        title: "Embeddings",
        topic: "general",
        concepts: ["embedding", "vector", "dimensions"],
        summary:
            "Embedding clients convert text into vectors whose configured dimensions must match the index.",
        sections: ["Model selection", "Dimensions", "Reindexing"],
    },
    {
        id: "handbook-document-stores",
        filename: "document-stores.md",
        title: "Document Stores",
        topic: "general",
        concepts: ["document store", "provider", "metadata"],
        summary:
            "Document-store adapters persist source documents and chunk metadata behind a shared contract.",
        sections: ["Memory", "File", "Provider adapters"],
    },
    {
        id: "handbook-vector-stores",
        filename: "vector-stores.md",
        title: "Vector Stores",
        topic: "general",
        concepts: ["vector store", "Elasticsearch", "index"],
        summary:
            "Vector-store adapters index embeddings and return candidates for the query pipeline.",
        sections: ["Memory", "Elasticsearch", "Filters"],
    },
    {
        id: "handbook-jobs",
        filename: "ingestion-jobs.md",
        title: "Async Ingestion Jobs",
        topic: "general",
        concepts: ["jobs", "ingestion", "progress"],
        summary:
            "Background ingestion jobs expose lifecycle state and document and chunk progress counts.",
        sections: ["Queue", "Progress", "Control"],
    },
    {
        id: "handbook-evaluation",
        filename: "evaluation-guide.md",
        title: "Evaluation",
        topic: "general",
        concepts: ["evaluation", "recall", "citations"],
        summary:
            "Deterministic evaluation measures retrieval and citation behavior against a golden dataset.",
        sections: ["Golden questions", "Metrics", "Regression floors"],
    },
    {
        id: "handbook-observability",
        filename: "observability.md",
        title: "Observability",
        topic: "general",
        concepts: ["observability", "trace", "latency"],
        summary:
            "Pipeline traces make stage timing, retrieval candidates, and context selection inspectable.",
        sections: ["Trace stages", "Latency", "Diagnostics"],
    },
    {
        id: "handbook-llm",
        filename: "llm-providers.md",
        title: "LLM Providers",
        topic: "general",
        concepts: ["LLM", "chat", "provider"],
        summary:
            "Embedding and chat clients are configured separately and can target compatible endpoints.",
        sections: ["Embedding client", "Chat client", "Grounding"],
    },
    {
        id: "handbook-citations",
        filename: "citations.md",
        title: "Grounded Citations",
        topic: "general",
        concepts: ["citations", "evidence", "answer"],
        summary:
            "Answers should cite retrieved evidence and abstain when the available context is insufficient.",
        sections: ["Evidence", "Citation links", "Abstention"],
    },
];

const productSeeds: DocumentSeed[] = [
    {
        id: "product-overview",
        filename: "product-overview.md",
        title: "Product Overview",
        topic: "general",
        concepts: ["product", "workspace", "search"],
        summary:
            "A workspace brings document ingestion and grounded search into one workflow.",
        sections: ["Workspace", "Search", "Administration"],
    },
    {
        id: "product-security",
        filename: "security-brief.md",
        title: "Security Brief",
        topic: "security",
        concepts: ["security", "encryption", "access"],
        summary:
            "Product access uses scoped identities and encrypted transport for service requests.",
        sections: ["Identity", "Encryption", "Audit"],
    },
    {
        id: "product-onboarding",
        filename: "launch-checklist.md",
        title: "Launch Checklist",
        topic: "onboarding",
        concepts: ["launch", "onboarding", "documents"],
        summary:
            "A launch begins with a small representative set of approved source documents.",
        sections: ["Prepare", "Configure", "Review"],
    },
    {
        id: "product-support",
        filename: "support-portal.md",
        title: "Support Portal",
        topic: "support",
        concepts: ["support", "case", "organization"],
        summary:
            "The support portal tracks product questions by organization and case identifier.",
        sections: ["Create a case", "Add context", "Track status"],
    },
    {
        id: "product-api",
        filename: "integration-api.md",
        title: "Integration API",
        topic: "api",
        concepts: ["api", "integration", "query"],
        summary:
            "The API accepts query requests and returns answers with evidence metadata.",
        sections: ["Authenticate", "Query", "Handle responses"],
    },
    {
        id: "product-admin",
        filename: "admin-guide.md",
        title: "Administration",
        topic: "general",
        concepts: ["admin", "workspace", "settings"],
        summary:
            "Workspace administrators manage membership, settings, and source connections.",
        sections: ["Members", "Settings", "Connections"],
    },
    {
        id: "product-ingest",
        filename: "ingestion-options.md",
        title: "Ingestion Options",
        topic: "onboarding",
        concepts: ["ingestion", "sources", "documents"],
        summary:
            "Ingestion options allow teams to select approved local or cloud document sources.",
        sections: ["Sources", "Formats", "Refresh"],
    },
    {
        id: "product-retrieval",
        filename: "retrieval-guide.md",
        title: "Retrieval Guide",
        topic: "general",
        concepts: ["retrieval", "ranking", "context"],
        summary:
            "Retrieval ranks relevant passages and assembles a bounded context for answering.",
        sections: ["Candidates", "Ranking", "Context"],
    },
    {
        id: "product-billing",
        filename: "billing-faq.md",
        title: "Billing FAQ",
        topic: "support",
        concepts: ["billing", "plan", "invoice"],
        summary:
            "Plan and invoice questions can be sent to the billing support queue.",
        sections: ["Plans", "Invoices", "Contact"],
    },
    {
        id: "product-data",
        filename: "data-controls.md",
        title: "Data Controls",
        topic: "security",
        concepts: ["data", "retention", "privacy"],
        summary:
            "Data controls describe retention, deletion, and administrative review options.",
        sections: ["Retention", "Deletion", "Review"],
    },
    {
        id: "product-eval",
        filename: "quality-evaluation.md",
        title: "Quality Evaluation",
        topic: "general",
        concepts: ["evaluation", "quality", "dataset"],
        summary:
            "Evaluation datasets help compare retrieval settings against expected evidence.",
        sections: ["Dataset", "Scoring", "Review"],
    },
    {
        id: "product-troubleshooting",
        filename: "troubleshooting.md",
        title: "Troubleshooting",
        topic: "support",
        concepts: ["troubleshooting", "support", "logs"],
        summary:
            "Support can diagnose a problem faster with its timestamp and request identifier.",
        sections: ["Collect details", "Check status", "Contact support"],
    },
];

const supportSeeds: DocumentSeed[] = [
    {
        id: "support-getting-started",
        filename: "getting-started.md",
        title: "Getting Started",
        topic: "onboarding",
        concepts: ["onboarding", "setup", "workspace"],
        summary:
            "The getting-started flow creates a workspace and guides the first source connection.",
        sections: ["Create workspace", "Connect source", "Ask"],
    },
    {
        id: "support-login",
        filename: "login-help.md",
        title: "Login Help",
        topic: "security",
        concepts: ["login", "identity", "authentication"],
        summary:
            "Use the configured identity provider and confirm the correct workspace before signing in.",
        sections: ["Sign in", "Multi-factor", "Recovery"],
    },
    {
        id: "support-upload",
        filename: "upload-help.md",
        title: "Upload Help",
        topic: "support",
        concepts: ["upload", "format", "documents"],
        summary:
            "Check file format and size guidance when an upload does not complete.",
        sections: ["Supported formats", "Upload status", "Retry"],
    },
    {
        id: "support-answers",
        filename: "answer-quality.md",
        title: "Improve Answer Quality",
        topic: "general",
        concepts: ["answer", "question", "citations"],
        summary:
            "Specific questions and current source material help produce clearer cited answers.",
        sections: ["Question wording", "Source coverage", "Citations"],
    },
    {
        id: "support-refunds",
        filename: "refund-faq.md",
        title: "Refund FAQ",
        topic: "refund",
        concepts: ["refund", "purchase", "billing"],
        summary:
            "Refund eligibility depends on the purchase channel and the applicable account terms.",
        sections: ["Eligibility", "Request", "Timing"],
    },
    {
        id: "support-api",
        filename: "api-help.md",
        title: "API Help",
        topic: "api",
        concepts: ["api", "request", "support"],
        summary:
            "Include the endpoint, response code, and request identifier when asking about API behavior.",
        sections: ["Request details", "Errors", "Contact"],
    },
    {
        id: "support-sync",
        filename: "sync-issues.md",
        title: "Source Sync Issues",
        topic: "support",
        concepts: ["sync", "source", "ingestion"],
        summary:
            "Review source permissions and job status when a document sync is incomplete.",
        sections: ["Permissions", "Job status", "Retry"],
    },
    {
        id: "support-access",
        filename: "access-help.md",
        title: "Workspace Access",
        topic: "security",
        concepts: ["access", "membership", "permissions"],
        summary:
            "Ask a workspace administrator to confirm membership and role permissions.",
        sections: ["Membership", "Roles", "Escalation"],
    },
    {
        id: "support-search",
        filename: "search-help.md",
        title: "Search Tips",
        topic: "general",
        concepts: ["search", "retrieval", "terms"],
        summary:
            "Use concrete product terms and names to make document search more precise.",
        sections: ["Choose terms", "Narrow scope", "Review matches"],
    },
    {
        id: "support-billing",
        filename: "payment-help.md",
        title: "Payment Help",
        topic: "support",
        concepts: ["payment", "invoice", "billing"],
        summary:
            "Payment questions should reference an invoice or transaction date without sharing card details.",
        sections: ["Invoice", "Payment", "Contact"],
    },
    {
        id: "support-privacy",
        filename: "privacy-help.md",
        title: "Privacy Help",
        topic: "security",
        concepts: ["privacy", "data", "request"],
        summary:
            "Privacy requests are routed to the privacy team for identity verification and tracking.",
        sections: ["Submit", "Verify", "Track"],
    },
    {
        id: "support-status",
        filename: "status-help.md",
        title: "Check Service Status",
        topic: "support",
        concepts: ["status", "incident", "availability"],
        summary:
            "Check current service notices before troubleshooting a possible platform incident.",
        sections: ["Notices", "Subscribe", "Report"],
    },
    {
        id: "support-config",
        filename: "configuration-help.md",
        title: "Configuration Help",
        topic: "onboarding",
        concepts: ["configuration", "provider", "settings"],
        summary:
            "Confirm selected providers and required settings when configuring a deployment.",
        sections: ["Provider", "Settings", "Validate"],
    },
    {
        id: "support-chunks",
        filename: "chunking-help.md",
        title: "Chunking Help",
        topic: "general",
        concepts: ["chunking", "content", "retrieval"],
        summary:
            "Review chunk boundaries when evidence spans headings or appears incomplete.",
        sections: ["Inspect", "Adjust", "Compare"],
    },
    {
        id: "support-keys",
        filename: "credential-help.md",
        title: "Credential Help",
        topic: "security",
        concepts: ["credential", "key", "rotation"],
        summary:
            "Rotate a credential through its provider and remove the old value after confirming access.",
        sections: ["Rotate", "Verify", "Report"],
    },
    {
        id: "support-latency",
        filename: "latency-help.md",
        title: "Latency Help",
        topic: "general",
        concepts: ["latency", "trace", "pipeline"],
        summary:
            "A stage trace helps identify whether query delay comes from embedding, retrieval, or generation.",
        sections: ["Inspect trace", "Compare", "Escalate"],
    },
    {
        id: "support-webhooks",
        filename: "webhook-help.md",
        title: "Webhook Help",
        topic: "api",
        concepts: ["webhook", "signature", "event"],
        summary:
            "Confirm signature validation and retry handling when webhook events are missing.",
        sections: ["Validate", "Retry", "Contact"],
    },
    {
        id: "support-retention",
        filename: "retention-help.md",
        title: "Retention Help",
        topic: "security",
        concepts: ["retention", "deletion", "data"],
        summary:
            "Retention and deletion behavior follows the configured provider and account policy.",
        sections: ["Policy", "Request", "Verify"],
    },
];

const sectionNames = [
    "Overview",
    "Requirements",
    "Procedure",
    "Review",
    "Operations",
    "Exceptions",
];

function makeChunkContent(
    seed: DocumentSeed,
    section: string,
    ordinal: number,
): string {
    const opening = `${seed.title} — ${section}. ${seed.summary}`;
    const detail = `For this step, the responsible owner checks the current workspace configuration, confirms the source or request is in scope, and records the outcome with its stable identifier. Keep the supporting evidence with the relevant document or trace so another operator can repeat the review without relying on assumptions.`;
    const guidance = [
        "Apply the documented setting consistently across environments and confirm that the selected provider is registered before processing begins.",
        "When a result is unclear, preserve the original input, inspect the stage diagnostics, and ask the owning team to resolve the ambiguity.",
        "Use least-privilege access, avoid copying credentials into notes, and retain only the metadata needed to explain the operational decision.",
        "A successful operation should leave a traceable status, an accurate count, and a clear next step for the person responsible for follow-up.",
    ][ordinal % 4];
    return `${opening}\n\n${detail} ${guidance} The procedure may vary by provider, so check the configured adapter and applicable agreement before making a customer-facing commitment.`;
}

function chunksForSeed(
    seed: DocumentSeed,
    corpusId: CorpusId,
    count: number,
    startIndex: number,
): DemoChunk[] {
    const output: DemoChunk[] = [];
    const sections = seed.sections.length > 0 ? seed.sections : sectionNames;
    for (let ordinal = 0; ordinal < count; ordinal += 1) {
        const section =
            sections[ordinal % sections.length] ??
            sectionNames[ordinal % sectionNames.length] ??
            "Overview";
        const content = makeChunkContent(seed, section, ordinal);
        const numericId = startIndex + ordinal;
        output.push({
            id:
                corpusId === "handbook" &&
                [19, 99, 128, 244, 301].includes(numericId)
                    ? `chunk-${numericId}`
                    : `${corpusId}-chunk-${String(numericId).padStart(4, "0")}`,
            documentId: seed.id,
            filename: seed.filename,
            section,
            index:
                corpusId === "handbook" &&
                [19, 99, 128, 244, 301].includes(numericId)
                    ? numericId
                    : ordinal,
            content,
            summary: seed.summary,
            concepts: [...seed.concepts],
            topic: seed.topic,
        });
    }
    return output;
}

const seedByCorpus: Record<CorpusId, DocumentSeed[]> = {
    handbook: handbookSeeds,
    product: productSeeds,
    support: supportSeeds,
};

const chunkCounts: Record<CorpusId, number> = {
    handbook: 1284,
    product: 240,
    support: 360,
};

const generated = (
    Object.entries(seedByCorpus) as [CorpusId, DocumentSeed[]][]
).flatMap(([corpusId, seeds]) => {
    const targetCount = chunkCounts[corpusId];
    const baseCount = Math.floor(targetCount / seeds.length);
    const remainder = targetCount % seeds.length;
    let corpusOffset = 0;
    return seeds.map((seed, seedIndex) => {
        const count = baseCount + (seedIndex < remainder ? 1 : 0);
        const ownChunks = chunksForSeed(seed, corpusId, count, corpusOffset);
        corpusOffset += count;
        return {
            document: {
                id: seed.id,
                corpusId,
                filename: seed.filename,
                title: seed.title,
                format:
                    seed.format ??
                    (seed.filename.endsWith(".txt") ? "txt" : "md"),
                content: "",
                chunkCount: ownChunks.length,
                sections: [...new Set(ownChunks.map((chunk) => chunk.section))],
            } satisfies CorpusDocument,
            chunks: ownChunks,
        };
    });
});

const generatedChunks = generated.flatMap((entry) => entry.chunks);
const handbookEvidence: Record<
    number,
    {
        documentId: string;
        filename: string;
        title: string;
        content: string;
        summary: string;
        concepts: string[];
    }
> = {
    128: {
        documentId: "handbook-refunds",
        filename: "handbook.md",
        title: "Refund Policy",
        summary:
            "Customers may request a full refund within 30 calendar days of purchase.",
        content:
            "The request must include the original order number and be submitted by the account owner through the support portal. The refund window begins on the purchase date shown on the receipt. Eligibility is assessed against the original purchase agreement. This policy applies to direct purchases; separate enterprise agreements and reseller transactions must be reviewed under their own terms. The support team records the request and confirms the applicable policy before approving payment.",
        concepts: ["refund", "purchase", "30 days"],
    },
    244: {
        documentId: "handbook-agreements",
        filename: "enterprise-agreements.md",
        title: "Enterprise Exceptions",
        summary:
            "Enterprise agreements may define different refund terms; the signed contract takes precedence over the standard policy.",
        content:
            "The account team must review the executed master services agreement and order form before processing a request. Contract-specific commitments may include custom cancellation windows, annual billing terms, and service credits. A customer success manager can confirm which terms govern a particular purchase. If there is a conflict between this handbook and a signed agreement, escalate to the contract owner for review. Do not promise the standard refund window until the agreement has been checked.",
        concepts: ["enterprise", "contract", "terms", "exceptions"],
    },
    19: {
        documentId: "handbook-services",
        filename: "handbook.md",
        title: "Non-refundable Services",
        summary:
            "Completed setup fees and custom implementation services are non-refundable, unless the signed agreement states otherwise.",
        content:
            "This exclusion covers work that has already been delivered, including custom integrations, migration assistance, and tailored training. Unused subscription access follows the standard refund policy unless an enterprise agreement overrides it. Before purchasing custom services, the account owner should confirm the statement of work and acceptance criteria. Disputed delivery or a billing error should be reviewed by customer support with the relevant order number and contract.",
        concepts: ["setup", "custom services", "non-refundable", "exceptions"],
    },
    301: {
        documentId: "handbook-refund-procedures",
        filename: "refund-procedures.md",
        title: "Refund Processing",
        summary:
            "Approved refunds are returned to the original payment method within five to ten business days.",
        content:
            "Processing begins after support approves the request and confirms eligibility. The customer receives an email confirmation with a reference number. Banks and payment providers may show a pending credit before settlement. If the credit has not appeared after ten business days, contact support with the approval reference. Do not submit another refund request while the original payment is being processed. The processing timeline is separate from the thirty-day eligibility window.",
        concepts: ["refund", "processing", "business days", "payment"],
    },
    99: {
        documentId: "handbook-reseller",
        filename: "reseller-policy.md",
        title: "Reseller Purchases",
        summary:
            "Reseller purchases may have a 60-day partner refund window, which does not override the policy for direct purchases.",
        content:
            "The partner named on the invoice is responsible for applying its own published terms. Customers who bought directly from the company should follow the standard thirty-day policy in the employee handbook. Verify the seller of record before quoting a refund window. A reseller contract must not be used as evidence for a direct-purchase refund. If the seller cannot be identified, support should review the receipt before responding with a policy decision.",
        concepts: ["reseller", "partner", "60 days", "refund"],
    },
};

for (const [numericId, evidence] of Object.entries(handbookEvidence)) {
    const chunk = generatedChunks[Number(numericId)];
    if (!chunk) continue;
    Object.assign(chunk, {
        id: `chunk-${numericId}`,
        index: Number(numericId),
        documentId: evidence.documentId,
        filename: evidence.filename,
        section: evidence.title,
        content: `${evidence.summary} ${evidence.content}`,
        summary: evidence.summary,
        concepts: evidence.concepts,
        topic: "refund" as const,
    });
}

const chunks: DemoChunk[] = generatedChunks;
const documents: CorpusDocument[] = generated.map(({ document }) => {
    const ownChunks = chunks.filter(
        (chunk) => chunk.documentId === document.id,
    );
    return {
        ...document,
        content: ownChunks.map((chunk) => chunk.content).join("\n\n"),
        chunkCount: ownChunks.length,
        sections: [...new Set(ownChunks.map((chunk) => chunk.section))],
    };
});

export { chunks };
export { documents };
export const corpora: Corpus[] = (Object.keys(seedByCorpus) as CorpusId[]).map(
    (id) => {
        const corpusDocuments = documents.filter(
            (document) => document.corpusId === id,
        );
        const corpusChunks = chunks.filter((chunk) =>
            corpusDocuments.some(
                (document) => document.id === chunk.documentId,
            ),
        );
        const label =
            id === "handbook"
                ? "Employee Handbook"
                : id === "product"
                  ? "Product Documentation"
                  : "Support Knowledge Base";
        return {
            id,
            name: label,
            description: `${label} demonstration corpus with deterministic local documents and retrieval chunks.`,
            documentCount: corpusDocuments.length,
            chunkCount: corpusChunks.length,
        };
    },
);

export const EXAMPLE_QUESTIONS: Record<CorpusId, string[]> = {
    handbook: [
        "What is the refund policy for a direct purchase?",
        "Do enterprise contract terms take precedence over the standard policy?",
        "Are setup fees and custom services refundable?",
        "How long does an approved refund take to process?",
        "Do reseller terms change the direct purchase refund window?",
        "How should I report a suspected security incident?",
        "Which API response details help diagnose a failed request?",
        "How do I inspect the evidence behind an answer?",
    ],
    product: [
        "How do I connect a source to a new workspace?",
        "What information should I include when contacting support?",
        "How does the API return evidence for an answer?",
        "What security controls protect workspace access?",
    ],
    support: [
        "How do I get started with a workspace?",
        "What details help support investigate an upload issue?",
        "How can I improve the quality of cited answers?",
        "Where can I check the current service status?",
    ],
};
