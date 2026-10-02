"use client";
import { useEffect, useState } from "react";
import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { ClientJob } from "@/lib/live/contracts";
import { liveGateway } from "@/lib/live/gateway";

export function RecentJobs({ kind, profileId, onSelect }: { kind: string; profileId?: string; onSelect: (job: ClientJob) => void }) {
    const [jobs, setJobs] = useState<ClientJob[]>([]);
    const [error, setError] = useState("");
    const [revision, setRevision] = useState(0);
    useEffect(() => {
        const controller = new AbortController();
        liveGateway.listJobs(controller.signal).then(items => { setJobs(items); setError(""); }).catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Job history unavailable."); });
        return () => controller.abort();
    }, [revision, profileId]);
    const filtered = jobs.filter(job => job.kind === kind && (!profileId || job.profileId === profileId)).slice(0, 20);
    return <div className="mt-5 border-t pt-4"><div className="flex items-center justify-between"><h3 className="eyebrow">Saved {kind} jobs</h3><Button size="sm" variant="ghost" aria-label={`Refresh ${kind} jobs`} onClick={() => setRevision(value => value + 1)}><RefreshCw size={12} /></Button></div>{error && <p role="alert" className="mt-2 text-[10px] text-destructive">{error}</p>}<div className="mt-2 max-h-48 space-y-2 overflow-auto">{filtered.map(job => <button className="flex w-full items-center gap-3 rounded border p-2.5 text-left text-[10px] hover:bg-accent" key={job.id} onClick={() => onSelect(job)}><span className="mono">{job.id.slice(0, 8)}</span><span>{job.completed}/{job.total}</span><span className="ml-auto capitalize">{job.status}</span></button>)}{!filtered.length && !error && <p className="text-[10px] text-muted-foreground">No saved jobs. Refresh after starting a run.</p>}</div></div>;
}
