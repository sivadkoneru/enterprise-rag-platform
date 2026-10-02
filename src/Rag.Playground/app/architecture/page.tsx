import { Cpu, Network } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { PageFooter, PageHeading, Panel } from "@/components/shared/panel";
import { ArchitectureCanvas } from "@/features/architecture/architecture-canvas";
import { EnvironmentView } from "@/features/live/environment-view";

export default function ArchitecturePage() {
    return (
        <EnvironmentView page="architecture"><div>
            <PageHeading
                eyebrow="Platform map"
                title="Architecture"
                description="Trace the backend ingestion and query paths, then inspect which ranking and diagnostics steps are only simulated in this Playground."
                action={
                    <Badge variant="outline" className="gap-1.5 px-3 py-1.5">
                        <Cpu size={12} className="text-primary" /> .NET 10 ·
                        adapter based
                    </Badge>
                }
            />
            <Panel
                className="mb-5 border-primary/15 bg-accent/35"
                bodyClassName="flex flex-wrap items-start gap-3 py-4"
            >
                <span className="rounded-md bg-card p-2 text-primary">
                    <Network size={16} />
                </span>
                <div className="min-w-0 flex-1">
                    <div className="text-xs font-semibold">
                        Explicit boundaries keep providers replaceable
                    </div>
                    <p className="text-muted mt-1 max-w-4xl text-[11px] leading-5">
                        Rag.Core owns contracts and pipeline orchestration.
                        Optional source and document-store adapters register
                        from separate provider packages; memory and
                        Elasticsearch vector stores are available in core.
                        Dashed gray connectors show supporting evaluation or
                        demo-diagnostics paths.
                    </p>
                </div>
            </Panel>
            <ArchitectureCanvas />
            <PageFooter />
        </div></EnvironmentView>
    );
}
