"use client";
import { Check, CircleDashed } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import type { DiagramNode } from "./architecture-data";
import { nodeHeight } from "./graph-layout";
export function NodeDetails({ node }: { node: DiagramNode }) {
    const implemented = node.implementation === "Implemented backend";
    return (
        <div className="space-y-5 pt-5">
            <div className="flex flex-wrap items-center gap-2">
                <Badge
                    variant={implemented ? "secondary" : "outline"}
                    className="gap-1.5"
                >
                    {implemented ? (
                        <Check size={11} className="text-emerald-600" />
                    ) : (
                        <CircleDashed size={11} className="text-primary" />
                    )}
                    {node.implementation}
                </Badge>
                <Badge variant="outline" className="capitalize">
                    {node.group}
                </Badge>
            </div>
            <p className="text-sm leading-6">{node.responsibility}</p>
            <div className="space-y-3 rounded-lg border bg-muted/40 p-4 text-xs leading-5">
                <div>
                    <div className="eyebrow mb-1">Technology</div>
                    <div>{node.technology}</div>
                </div>
                <div>
                    <div className="eyebrow mb-1">Inputs</div>
                    <div>{node.inputs}</div>
                </div>
                <div>
                    <div className="eyebrow mb-1">Outputs</div>
                    <div>{node.outputs}</div>
                </div>
            </div>
            <div>
                <div className="eyebrow mb-2">Operational notes</div>
                <ul className="space-y-2 text-xs leading-5 text-muted-foreground">
                    {node.considerations.map((item) => (
                        <li key={item} className="flex gap-2">
                            <span className="mt-1.5 size-1 shrink-0 rounded-full bg-primary" />
                            {item}
                        </li>
                    ))}
                </ul>
            </div>
        </div>
    );
}

export function ArchitectureNodeButton({
    node,
    onSelect,
    fit = false,
}: {
    node: DiagramNode;
    onSelect: (node: DiagramNode) => void;
    fit?: boolean;
}) {
    const implemented = node.implementation === "Implemented backend";
    return (
        <button
            type="button"
            aria-label={`Inspect ${node.name}, ${node.implementation}`}
            onClick={() => onSelect(node)}
            className={`flex flex-col items-start justify-center rounded-lg border bg-card px-3 text-left shadow-sm transition hover:border-primary hover:shadow-md focus-visible:ring-2 focus-visible:ring-ring ${implemented ? "" : "border-dashed bg-accent/25"}`}
            style={{ width: "100%", height: fit ? "100%" : nodeHeight }}
        >
            <span className="flex items-center gap-1.5 text-[11px] font-semibold leading-4">
                <span
                    className={`size-1.5 shrink-0 rounded-full ${implemented ? "bg-emerald-600" : "bg-primary"}`}
                />
                {node.name}
            </span>
            <span className="mt-1 text-[9px] leading-3 text-muted-foreground">
                {node.implementation}
            </span>
        </button>
    );
}
