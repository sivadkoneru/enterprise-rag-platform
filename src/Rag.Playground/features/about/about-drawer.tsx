"use client";

import Link from "next/link";
import {
    ArrowUpRight,
    BookOpenCheck,
    Code2,
    GitMerge,
    Gauge,
    Layers3,
    SearchCheck,
    ShieldCheck,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Drawer } from "@/components/shared/drawer";

export function AboutDrawer({
    open,
    onOpenChange,
}: {
    open: boolean;
    onOpenChange: (open: boolean) => void;
}) {
    return (
        <Drawer
            open={open}
            onOpenChange={onOpenChange}
            title="About the Playground"
            description="A local, inspectable demo of the Enterprise RAG Platform."
        >
            <div className="space-y-6 pt-5">
                <div>
                    <div className="eyebrow mb-2">Purpose</div>
                    <p className="text-sm leading-6 text-muted-foreground">
                        Explore how the .NET platform ingests documents,
                        retrieves evidence, and returns answers with source
                        citations. This browser Playground uses local
                        deterministic fixtures; it does not call the platform
                        API or an LLM service.
                    </p>
                </div>

                <div>
                    <div className="eyebrow mb-3">Platform capabilities</div>
                    <div className="space-y-2.5">
                        {[
                            {
                                icon: Layers3,
                                title: "Composable ingestion",
                                detail: "Sources, parsers, chunking strategies, embeddings, and stores sit behind explicit contracts.",
                            },
                            {
                                icon: SearchCheck,
                                title: "Retrieval experiments",
                                detail: "Compare strategies and inspect ranked evidence, filters, and citation references in the local demo.",
                            },
                            {
                                icon: Gauge,
                                title: "Latency & cost observability",
                                detail: "Compare simulated query timing with benchmark measures such as embedding calls and context tokens; these are not live telemetry or dollar billing.",
                            },
                            {
                                icon: ShieldCheck,
                                title: "Opt-in providers",
                                detail: "Cloud sources and database adapters live in separate packages with explicit registration.",
                            },
                            {
                                icon: BookOpenCheck,
                                title: "Evaluation harness",
                                detail: "The offline deterministic golden dataset measures retrieval and citation behavior, not production answer quality.",
                            },
                        ].map(({ icon: Icon, title, detail }) => (
                            <div
                                key={title}
                                className="flex gap-3 rounded-lg border p-3"
                            >
                                <span className="mt-0.5 rounded-md bg-accent p-2 text-accent-foreground">
                                    <Icon size={14} />
                                </span>
                                <div>
                                    <div className="text-xs font-semibold">
                                        {title}
                                    </div>
                                    <p className="text-muted mt-1 text-[11px] leading-4">
                                        {detail}
                                    </p>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>

                <div>
                    <div className="eyebrow mb-3">Explore this demo</div>
                    <div className="grid gap-2">
                        <Link
                            href="/architecture"
                            onClick={() => onOpenChange(false)}
                            className="flex items-center justify-between rounded-lg border px-3 py-2.5 text-xs font-medium hover:bg-muted"
                        >
                            View Architecture <ArrowUpRight size={13} />
                        </Link>
                        <Link
                            href="/evaluation"
                            onClick={() => onOpenChange(false)}
                            className="flex items-center justify-between rounded-lg border px-3 py-2.5 text-xs font-medium hover:bg-muted"
                        >
                            View Evaluation <ArrowUpRight size={13} />
                        </Link>
                    </div>
                </div>

                <a
                    href="https://github.com/sivadkoneru/enterprise-rag-platform"
                    target="_blank"
                    rel="noreferrer"
                    className="flex items-center justify-between rounded-lg bg-foreground px-4 py-3 text-background transition hover:opacity-90"
                >
                    <span className="flex items-center gap-2 text-xs font-medium">
                        <Code2 size={15} /> View GitHub Repository
                    </span>
                    <ArrowUpRight size={13} />
                </a>

                <div className="flex flex-wrap items-center gap-2 border-t pt-4 text-[10px] text-muted-foreground">
                    <Badge variant="outline">.NET 10</Badge>
                    <Badge variant="outline">Local fixtures</Badge>
                    <span>Retrieval and citations, made inspectable.</span>
                    <GitMerge size={12} />
                </div>
            </div>
        </Drawer>
    );
}
