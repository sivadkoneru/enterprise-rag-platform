"use client";
import { useEffect, useRef, useState } from "react";
import { CheckCircle2, Loader2, Pause, Play, Square } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { ClientJob } from "@/lib/live/contracts";
import { liveGateway } from "@/lib/live/gateway";

export function JobProgress({ initial, onComplete }: { initial: ClientJob; onComplete: (job: ClientJob) => void }) {
    const [job, setJob] = useState(initial);
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const completeRef = useRef(onComplete);
    const notified = useRef(false);
    useEffect(() => { completeRef.current = onComplete; }, [onComplete]);
    useEffect(() => {
        const controller = new AbortController();
        let timer: ReturnType<typeof setTimeout>;
        async function poll() {
            try {
                const next = await liveGateway.getJob(initial.id, controller.signal);
                if (controller.signal.aborted) return;
                setJob(next); setError("");
                if (["complete", "completed", "failed", "canceled"].includes(next.status)) {
                    if (!notified.current) { notified.current = true; completeRef.current(next); }
                    return;
                }
            } catch (err) { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Job status unavailable."); }
            if (!controller.signal.aborted) timer = setTimeout(() => void poll(), 1500);
        }
        void poll();
        return () => { controller.abort(); clearTimeout(timer); };
    }, [initial.id]);
    async function control(action: "pause" | "resume" | "cancel") {
        setBusy(true);
        try { setJob(await liveGateway.controlJob(job.id, action)); }
        catch (err) { setError(err instanceof Error ? err.message : "Job action failed."); }
        finally { setBusy(false); }
    }
    const terminal = ["complete", "completed", "failed", "canceled"].includes(job.status);
    return <div className="rounded-lg border bg-muted/20 p-4" aria-live="polite">
        <div className="flex flex-wrap items-center gap-2 text-xs font-medium">{terminal ? <CheckCircle2 size={14} /> : <Loader2 size={14} className={job.status === "paused" ? "" : "animate-spin"} />}<span className="capitalize">{job.kind} · {job.status}</span><span className="mono ml-auto text-[10px]">{job.completed} / {job.total}</span></div>
        <progress className="mt-3 h-1.5 w-full accent-primary" max={Math.max(1, job.total)} value={job.completed} aria-label="Job progress" />
        <p className="mono my-2 break-all text-[9px] text-muted-foreground">{job.id}</p>
        {!terminal && <div className="mt-3 flex gap-2"><Button variant="outline" size="sm" disabled={busy} onClick={() => void control(job.status === "paused" ? "resume" : "pause")}>{job.status === "paused" ? <Play size={12} /> : <Pause size={12} />}{job.status === "paused" ? "Resume" : "Pause"}</Button><Button variant="ghost" size="sm" disabled={busy} onClick={() => void control("cancel")}><Square size={12} />Cancel</Button></div>}
        {(error || job.error) && <p role="alert" className="mt-2 text-xs text-destructive">{error || job.error}</p>}
    </div>;
}
