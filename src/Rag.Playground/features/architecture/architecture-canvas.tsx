"use client";

import { useState } from "react";
import {
    ArrowDownRight,
    ArrowRight,
    Boxes,
    Check,
    CircleDashed,
    Database,
    GitBranch,
    Workflow,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Drawer } from "@/components/shared/drawer";
import { Panel } from "@/components/shared/panel";
import {
    architectureEdges,
    architectureNodes,
    diagramSize,
    type ArchitectureGroup,
    type DiagramNode,
} from "@/features/architecture/architecture-data";

import { connectorPath, nodeWidth, nodeHeight } from "./graph-layout";
import { NodeDetails, ArchitectureNodeButton } from "./architecture-node";
const groups: { id: ArchitectureGroup; label: string }[] = [
    { id: "ingestion", label: "Ingestion" },
    { id: "query", label: "Query" },
    { id: "operations", label: "Operations" },
    { id: "quality", label: "Quality" },
];

export function ArchitectureCanvas() {
    const [selected, setSelected] = useState<DiagramNode | null>(null);
    const [detailsOpen, setDetailsOpen] = useState(false);
    function selectNode(node: DiagramNode) {
        setSelected(node);
        setDetailsOpen(true);
    }
    const nodeById = new Map(architectureNodes.map((node) => [node.id, node]));

    return (
        <>
            <Panel className="overflow-hidden" bodyClassName="!p-0">
                <div className="flex flex-wrap items-center justify-between gap-3 border-b px-5 py-4">
                    <div>
                        <div className="panel-title">
                            <Workflow size={15} className="text-primary" />{" "}
                            Platform flow
                        </div>
                        <p className="text-muted mt-1 text-[11px]">
                            Select a component to inspect its role, inputs,
                            outputs, and implementation status.
                        </p>
                    </div>
                    <div className="flex flex-wrap gap-2 text-[10px]">
                        <Badge variant="outline" className="gap-1.5">
                            <Check size={11} className="text-emerald-600" />
                            Implemented backend
                        </Badge>
                        <Badge variant="outline" className="gap-1.5">
                            <CircleDashed size={11} className="text-primary" />
                            Simulated in demo
                        </Badge>
                    </div>
                </div>
                <div className="hidden overflow-x-auto md:block">
                    <div
                        className="relative min-w-[1000px] bg-[linear-gradient(to_right,transparent_0,transparent_19.9%,var(--border)_20%,transparent_20.1%,transparent_39.9%,var(--border)_40%,transparent_40.1%,transparent_59.9%,var(--border)_60%,transparent_60.1%,transparent_79.9%,var(--border)_80%,transparent_80.1%)]"
                        style={{
                            aspectRatio: `${diagramSize.width} / ${diagramSize.height}`,
                        }}
                    >
                        <div className="absolute left-4 top-3 z-10 flex items-center gap-2 text-[9px] font-semibold uppercase tracking-[.14em] text-muted-foreground">
                            <Boxes size={12} /> Ingestion path
                        </div>
                        <div className="absolute bottom-3 left-4 z-10 flex items-center gap-2 text-[9px] font-semibold uppercase tracking-[.14em] text-muted-foreground">
                            <GitBranch size={12} /> Query, operations & quality
                        </div>
                        <svg
                            className="absolute inset-0 size-full"
                            viewBox={`0 0 ${diagramSize.width} ${diagramSize.height}`}
                            fill="none"
                            aria-hidden="true"
                        >
                            <defs>
                                <marker
                                    id="architecture-arrow"
                                    markerWidth="7"
                                    markerHeight="7"
                                    refX="6"
                                    refY="3.5"
                                    orient="auto"
                                >
                                    <path
                                        d="M0 0L7 3.5L0 7"
                                        fill="var(--primary)"
                                    />
                                </marker>
                                <marker
                                    id="architecture-support-arrow"
                                    markerWidth="7"
                                    markerHeight="7"
                                    refX="6"
                                    refY="3.5"
                                    orient="auto"
                                >
                                    <path
                                        d="M0 0L7 3.5L0 7"
                                        fill="var(--muted-foreground)"
                                    />
                                </marker>
                            </defs>
                            {architectureEdges.map((edge) => {
                                const from = nodeById.get(edge.from);
                                const to = nodeById.get(edge.to);
                                if (!from || !to) return null;
                                const supporting = edge.kind === "supporting";
                                return (
                                    <path
                                        key={`${edge.from}-${edge.to}`}
                                        d={connectorPath(from, to)}
                                        stroke={
                                            supporting
                                                ? "var(--muted-foreground)"
                                                : "var(--primary)"
                                        }
                                        strokeOpacity={
                                            supporting ? "0.65" : "0.42"
                                        }
                                        strokeWidth="1.6"
                                        strokeDasharray={
                                            supporting ? "5 4" : undefined
                                        }
                                        markerEnd={`url(#architecture-${supporting ? "support-arrow" : "arrow"})`}
                                    >
                                        <title>{edge.label}</title>
                                    </path>
                                );
                            })}
                        </svg>
                        {architectureNodes.map((node) => (
                            <div
                                key={node.id}
                                className="absolute z-10"
                                style={{
                                    left: `${(node.x / diagramSize.width) * 100}%`,
                                    top: `${(node.y / diagramSize.height) * 100}%`,
                                    width: `${(nodeWidth / diagramSize.width) * 100}%`,
                                    height: `${(nodeHeight / diagramSize.height) * 100}%`,
                                    transform: "translate(-50%, -50%)",
                                }}
                            >
                                <ArchitectureNodeButton
                                    node={node}
                                    onSelect={selectNode}
                                    fit
                                />
                            </div>
                        ))}
                    </div>
                </div>
                <div className="grid gap-4 p-4 md:hidden">
                    {groups.map(({ id, label }) => {
                        const nodes = architectureNodes.filter(
                            (node) => node.group === id,
                        );
                        return (
                            <section
                                key={id}
                                aria-label={`${label} components`}
                            >
                                <div className="eyebrow mb-2">{label}</div>
                                <div className="grid gap-2 sm:grid-cols-2">
                                    {nodes.map((node) => (
                                        <ArchitectureNodeButton
                                            key={node.id}
                                            node={node}
                                            onSelect={selectNode}
                                        />
                                    ))}
                                </div>
                            </section>
                        );
                    })}
                </div>
            </Panel>

            <div className="mt-5 grid gap-4 lg:grid-cols-3">
                <Panel
                    title="Ingestion"
                    icon={<Database size={14} className="text-primary" />}
                    bodyClassName="space-y-3 text-xs leading-5 text-muted-foreground"
                >
                    <p>
                        Registered sources feed parser and chunking adapters.
                        Chunk text is embedded and indexed while documents and
                        metadata remain available for hydration.
                    </p>
                    <div className="flex items-center gap-2 text-[10px] text-foreground">
                        <span>Source</span>
                        <ArrowRight size={12} />
                        <span>Parse</span>
                        <ArrowRight size={12} />
                        <span>Chunk</span>
                        <ArrowRight size={12} />
                        <span>Index</span>
                    </div>
                </Panel>
                <Panel
                    title="Query path"
                    icon={<ArrowDownRight size={14} className="text-primary" />}
                    bodyClassName="space-y-3 text-xs leading-5 text-muted-foreground"
                >
                    <p>
                        The backend embeds a question, retrieves and hydrates
                        vector matches, then sends them to the chat client.
                        Extra ranking and context controls shown here are demo
                        simulations.
                    </p>
                    <div className="flex items-center gap-2 text-[10px] text-foreground">
                        <span>Question</span>
                        <ArrowRight size={12} />
                        <span>Matches</span>
                        <ArrowRight size={12} />
                        <span>Answer + citations</span>
                    </div>
                </Panel>
                <Panel
                    title="Operations & quality"
                    icon={<GitBranch size={14} className="text-primary" />}
                    bodyClassName="space-y-3 text-xs leading-5 text-muted-foreground"
                >
                    <p>
                        Ingestion jobs and the offline evaluation harness are
                        implemented backend capabilities. Query trace timing
                        shown in this Playground is simulated locally.
                    </p>
                    <div className="flex flex-wrap gap-1.5">
                        {[
                            "Async jobs",
                            "Demo diagnostics",
                            "Golden dataset",
                        ].map((name) => (
                            <Badge key={name} variant="secondary">
                                {name}
                            </Badge>
                        ))}
                    </div>
                </Panel>
            </div>

            <Drawer
                open={detailsOpen}
                onOpenChange={setDetailsOpen}
                title={selected?.name ?? "Component details"}
                description={
                    selected?.technology ?? "Architecture component details"
                }
            >
                {selected && <NodeDetails node={selected} />}
            </Drawer>
        </>
    );
}
