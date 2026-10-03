"use client";
import Link from "next/link";
import { Database, ExternalLink, RefreshCw, ServerOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Panel } from "@/components/shared/panel";
import type { ClientCorpus, IndexProfile } from "@/lib/live/contracts";

export function ClientError({ message, retry }: { message: string; retry?: () => void }) {
    return <div role="alert" className="mb-5 rounded-lg border border-destructive/25 bg-destructive/5 p-5 text-xs">
        <div className="flex items-center gap-2 font-semibold"><ServerOff size={16} /> Client connection needs attention</div>
        <p className="my-3 leading-6 text-muted-foreground">{message}</p>
        <div className="flex gap-3"><Button asChild variant="outline" size="sm"><Link href="/integrations">Open setup <ExternalLink size={12} /></Link></Button>{retry && <Button variant="ghost" size="sm" onClick={retry}><RefreshCw size={12} />Retry</Button>}</div>
    </div>;
}
export function ProfileSelectors({ corpora, profiles, corpusId, profileId, onCorpus, onProfile, disabled }: {
    corpora: ClientCorpus[]; profiles: IndexProfile[]; corpusId: string; profileId: string;
    onCorpus: (value: string) => void; onProfile: (value: string) => void; disabled?: boolean;
}) {
    const profile = profiles.find(item => item.id === profileId);
    return <div className="space-y-4">
        <label className="block"><span className="field-label">Document corpus</span><select className="field" value={corpusId} onChange={e => onCorpus(e.target.value)} disabled={disabled || !corpora.length}><option value="" disabled>Select a corpus</option>{corpora.map(c => <option value={c.id} key={c.id}>{c.name}</option>)}</select></label>
        <label className="block"><span className="field-label">Indexing profile</span><select className="field" value={profileId} onChange={e => onProfile(e.target.value)} disabled={disabled || !profiles.length}><option value="" disabled>Select a profile</option>{profiles.map(p => <option value={p.id} key={p.id}>{p.name} · {p.strategy}</option>)}</select></label>
        {profile && <div className="rounded-md border bg-muted/30 p-3 text-[10px] leading-6 text-muted-foreground"><span className="mono">{profile.documentCount} documents · {profile.chunkCount} chunks</span><br />{profile.embeddingModel} · {profile.embeddingDimensions} dimensions<br />{profile.chunkSize} characters · {profile.chunkOverlap} overlap<br /><span className="capitalize">{profile.status}</span> · Index settings are immutable</div>}
    </div>;
}
export function NoClientData() {
    return <Panel><div className="py-10 text-center"><Database className="mx-auto mb-4 text-primary" size={28} /><h2 className="text-sm font-semibold">Bring your evidence into the workspace</h2><p className="mx-auto my-3 max-w-md text-xs leading-6 text-muted-foreground">Create a corpus and indexing profile, then ingest your local files or cloud sources. Your actual documents will appear here.</p><Button asChild size="sm"><Link href="/integrations">Configure and ingest data</Link></Button></div></Panel>;
}
export function ClientFooter() {
    return <footer className="page-footer"><span>Client Environment · Results from your configured services</span><span>Query history stays in browser memory; evaluation reports persist on the server</span></footer>;
}
