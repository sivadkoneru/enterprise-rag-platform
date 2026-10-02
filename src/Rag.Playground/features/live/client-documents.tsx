"use client";
import { useEffect, useState } from "react";
import { ArrowLeft, ArrowRight, FileText } from "lucide-react";
import { Drawer } from "@/components/shared/drawer";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import type { ClientDocument, PageResult } from "@/lib/live/contracts";
import { liveGateway } from "@/lib/live/gateway";

export function ClientDocumentContent({ document }: { document: ClientDocument }) {
    return <article className="py-5"><div className="mb-4 flex items-center gap-2 text-xs"><FileText size={16} className="text-primary" /><strong>{document.filename}</strong><span className="ml-auto text-muted-foreground">{document.chunkCount} chunks</span></div><pre className="whitespace-pre-wrap break-words rounded-lg border bg-muted/30 p-4 font-sans text-xs leading-7">{document.content}</pre></article>;
}
export function ClientDocuments({ profileId, documentId, onClose }: { profileId: string; documentId?: string; onClose: () => void }) {
    const [page, setPage] = useState<PageResult<ClientDocument> | null>(null);
    const [selected, setSelected] = useState<ClientDocument | null>(null);
    const [offset, setOffset] = useState(0);
    const [error, setError] = useState("");
    useEffect(() => {
        const controller = new AbortController();
        (documentId ? liveGateway.getDocument(profileId, documentId, controller.signal).then(setSelected) : liveGateway.listDocuments(profileId, offset, controller.signal).then(setPage))
            .catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Document unavailable."); });
        return () => controller.abort();
    }, [profileId, documentId, offset]);
    async function select(id: string) {
        try { setSelected(await liveGateway.getDocument(profileId, id)); } catch (err) { setError(err instanceof Error ? err.message : "Document unavailable."); }
    }
    return <Drawer open onOpenChange={open => { if (!open) onClose(); }} title={selected?.filename ?? "Corpus documents"} description="Actual source text from the selected client indexing profile.">
        {error && <p role="alert" className="py-4 text-xs text-destructive">{error}</p>}
        {selected ? <>{!documentId && <Button variant="ghost" size="sm" className="mt-4" onClick={() => setSelected(null)}><ArrowLeft size={12} />Back to documents</Button>}<ClientDocumentContent document={selected} /></> : page ? <div className="space-y-3 py-5">{page.items.map(doc => <button key={doc.id} onClick={() => void select(doc.id)} className="flex w-full items-center gap-3 rounded-lg border p-4 text-left text-xs hover:bg-accent"><FileText size={15} className="text-primary" /><span className="break-all">{doc.filename}</span><span className="mono ml-auto shrink-0 text-[10px]">{doc.chunkCount} chunks</span></button>)}{!page.items.length && <p className="py-6 text-xs text-muted-foreground">No documents have been indexed in this profile.</p>}<div className="flex items-center justify-between pt-3 text-xs"><Button variant="outline" size="sm" disabled={offset === 0} onClick={() => setOffset(Math.max(0, offset - 20))}><ArrowLeft size={12} />Previous</Button><span>{page.total} documents</span><Button variant="outline" size="sm" disabled={offset + page.items.length >= page.total} onClick={() => setOffset(offset + 20)}>Next<ArrowRight size={12} /></Button></div></div> : !error && <Skeleton className="mt-6 h-40" />}
    </Drawer>;
}
