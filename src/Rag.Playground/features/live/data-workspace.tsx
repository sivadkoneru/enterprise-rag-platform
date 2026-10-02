"use client";
import { useState } from "react";
import Link from "next/link";
import { ArrowRight, Database, FileUp, Plus } from "lucide-react";
import { useEnvironment } from "@/components/shell/environment";
import { Panel } from "@/components/shared/panel";
import { Button } from "@/components/ui/button";
import { STRATEGY_LABELS } from "@/lib/constants";
import type { ChunkingStrategy } from "@/lib/contracts";
import type { ClientJob } from "@/lib/live/contracts";
import { liveGateway } from "@/lib/live/gateway";
import { useClientData } from "./use-client-data";
import { ClientError, ProfileSelectors } from "./shared";
import { RecentJobs } from "./recent-jobs";
import { JobProgress } from "./job-progress";
import { ClientDocuments } from "./client-documents";

export function DataWorkspace() {
    const { mode, setMode } = useEnvironment();
    return <section className="mt-8" aria-label="Client data workspace"><div className="mb-4 flex items-center gap-3"><span className="flex h-8 w-8 items-center justify-center rounded-md bg-accent text-primary"><Database size={16} /></span><div><h2 className="text-base font-semibold">Your data, end to end</h2><p className="mt-1 text-xs text-muted-foreground">Create an index, ingest documents, and query your evidence.</p></div></div>{mode === "client" ? <ConnectedDataWorkspace /> : <Panel><p className="mb-4 text-xs leading-6 text-muted-foreground">After launching your local stack, switch to Client Environment to connect to your API and ingest data.</p><Button size="sm" onClick={() => setMode("client")}>Use Client Environment<ArrowRight size={12} /></Button></Panel>}</section>;
}
function ConnectedDataWorkspace() {
    const data = useClientData();
    const [name, setName] = useState("");
    const [profileName, setProfileName] = useState("Recursive baseline");
    const [strategy, setStrategy] = useState<ChunkingStrategy>("recursive");
    const [size, setSize] = useState(800);
    const [overlap, setOverlap] = useState(120);
    const [sources, setSources] = useState("/documents");
    const [job, setJob] = useState<ClientJob | null>(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [browse, setBrowse] = useState(false);
    async function perform(action: () => Promise<void>) {
        setBusy(true); setError("");
        try { await action(); data.refresh(); } catch (err) { setError(err instanceof Error ? err.message : "Action failed."); }
        finally { setBusy(false); }
    }
    return <>{(error || data.error) && <ClientError message={error || data.error} retry={data.refresh} />}<div className="grid items-start gap-5 xl:grid-cols-3">
        <Panel title="1. Corpus" icon={<Database size={14} />}><form onSubmit={e => { e.preventDefault(); void perform(async () => { const corpus = await liveGateway.createCorpus(name.trim(), "Client document corpus"); data.setCorpusId(corpus.id); setName(""); }); }} className="space-y-3"><label className="block"><span className="field-label">New corpus name</span><input className="field" required maxLength={100} value={name} onChange={e => setName(e.target.value)} placeholder="e.g. Engineering documentation" /></label><Button type="submit" size="sm" variant="outline" disabled={busy || !data.capabilities}><Plus size={12} />Create corpus</Button></form><div className="mt-5 border-t pt-5"><ProfileSelectors {...data} onCorpus={data.setCorpusId} onProfile={data.setProfileId} disabled={busy} /></div></Panel>
        <Panel title="2. Indexing profile"><form className="space-y-4" onSubmit={e => { e.preventDefault(); void perform(async () => { if (!data.capabilities) return; const profile = await liveGateway.createProfile(data.corpusId, { name: profileName, strategy, chunkSize: size, chunkOverlap: overlap, embeddingModel: data.capabilities.embeddingModel, embeddingDimensions: data.capabilities.embeddingDimensions }); data.setProfileId(profile.id); }); }}><label className="block"><span className="field-label">Profile name</span><input required maxLength={100} className="field" value={profileName} onChange={e => setProfileName(e.target.value)} /></label><label className="block"><span className="field-label">Chunking strategy</span><select className="field" value={strategy} onChange={e => setStrategy(e.target.value as ChunkingStrategy)}>{Object.entries(STRATEGY_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><div className="grid grid-cols-2 gap-3"><label><span className="field-label">Size (characters)</span><input className="field" type="number" required min={200} max={16000} value={size} onChange={e => setSize(Number(e.target.value))} /></label><label><span className="field-label">Overlap</span><input className="field" type="number" required min={0} max={Math.max(0, size - 1)} value={overlap} onChange={e => setOverlap(Number(e.target.value))} /></label></div><p className="text-[11px] leading-6 text-muted-foreground">Uses the configured embedding model and dimensions. Each profile owns an isolated index. Changing indexing settings requires a new profile and ingestion.</p><Button type="submit" size="sm" disabled={busy || !data.corpusId}><Plus size={12} />Create profile</Button></form></Panel>
        <Panel title="3. Ingest sources" icon={<FileUp size={14} />}><form className="space-y-4" onSubmit={e => { e.preventDefault(); void perform(async () => { setJob(await liveGateway.ingest(data.profileId, sources.split(/\r?\n/).map(s => s.trim()).filter(Boolean))); }); }}><label className="block"><span className="field-label">Sources, one per line</span><textarea className="field mono min-h-28 !text-[11px]" required value={sources} onChange={e => setSources(e.target.value)} /></label><p className="text-[11px] leading-6 text-muted-foreground">Use container paths such as <code>/documents</code>, <code>s3://bucket/prefix</code>, or your configured Azure Blob URI. Sources must match server allowlists.</p><Button type="submit" size="sm" disabled={busy || !data.profileId}><FileUp size={12} />Start ingestion</Button></form><div className="mt-4 flex flex-wrap gap-2"><Button variant="outline" size="sm" disabled={!data.profileId} onClick={() => setBrowse(true)}>Browse documents</Button><Button asChild variant="ghost" size="sm"><Link href="/">Open Playground<ArrowRight size={12} /></Link></Button></div>{job && <div className="mt-4"><JobProgress key={job.id} initial={job} onComplete={next => { setJob(next); data.refresh(); }} /></div>}<RecentJobs kind="ingestion" profileId={data.profileId} onSelect={setJob} /></Panel>
    </div>{browse && data.profileId && <ClientDocuments profileId={data.profileId} onClose={() => setBrowse(false)} />}</>;
}
