"use client";
import { useEffect, useState } from "react";
import { ArrowLeft, FileText, Search } from "lucide-react";
import type { Corpus, CorpusDocument, CorpusId } from "@/lib/contracts";
import { gateway } from "@/lib/demo/gateway";
import { Drawer } from "@/components/shared/drawer";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

function DocumentContent({ document }: { document: CorpusDocument }) {
    return (
        <div className="pt-5">
            <div className="mb-5 flex flex-wrap gap-2">
                <Badge variant="secondary">
                    {document.format.toUpperCase()}
                </Badge>
                <Badge variant="outline">{document.chunkCount} chunks</Badge>
                <Badge variant="outline">Synthetic demo document</Badge>
            </div>
            <h3 className="mb-3 text-lg font-semibold">{document.title}</h3>
            <div className="mb-5 flex flex-wrap gap-1.5">
                {document.sections.map((section) => (
                    <span
                        className="rounded border bg-muted px-2 py-1 text-[10px]"
                        key={section}
                    >
                        {section}
                    </span>
                ))}
            </div>
            <div className="whitespace-pre-wrap rounded-lg border bg-muted/30 p-4 text-xs leading-7">
                {document.content}
            </div>
        </div>
    );
}
export function DocumentDrawer({
    documentId,
    onClose,
}: {
    documentId: string | null;
    onClose: () => void;
}) {
    const [document, setDocument] = useState<CorpusDocument | null>(null);
    const [error, setError] = useState("");
    useEffect(() => {
        if (!documentId) return;
        let active = true;
        gateway.getDocument(documentId).then(doc => { if (active) { setDocument(doc); setError(""); } }).catch(() => { if (active) setError("Document unavailable."); });
        return () => { active = false; };
    }, [documentId]);
    const currentDocument = document?.id === documentId ? document : null;
    return (
        <Drawer
            open={documentId !== null}
            onOpenChange={(open) => {
                if (!open) onClose();
            }}
            title={currentDocument?.filename ?? "Document"}
            description="Full source text from the synthetic demo corpus."
        >
            {currentDocument ? (
                <DocumentContent document={currentDocument} />
            ) : (
                <p className="py-8">{error || "Loading document…"}</p>
            )}
        </Drawer>
    );
}
export function CorpusDrawer({
    corpusId,
    open,
    onOpenChange,
}: {
    corpusId: CorpusId;
    open: boolean;
    onOpenChange: (value: boolean) => void;
}) {
    const [search, setSearch] = useState("");
    const [selected, setSelected] = useState<CorpusDocument | null>(null);
    const [corpus, setCorpus] = useState<Corpus | null>(null);
    const [documents, setDocuments] = useState<CorpusDocument[]>([]);
    const [error, setError] = useState("");
    useEffect(() => {
        if (!open) return;
        let active = true;
        Promise.all([gateway.listCorpora(), gateway.listDocuments(corpusId)]).then(([corpora, docs]) => { if (active) { setCorpus(corpora.find(item => item.id === corpusId) ?? null); setDocuments(docs); setError(""); } }).catch(() => { if (active) setError("Corpus could not be loaded."); });
        return () => { active = false; };
    }, [corpusId, open]);
    const filtered = documents.filter(
        (doc) =>
            doc.corpusId === corpusId &&
            `${doc.filename} ${doc.title}`
                .toLowerCase()
                .includes(search.toLowerCase()),
    );
    return (
        <Drawer
            open={open}
            onOpenChange={(value) => {
                onOpenChange(value);
                if (!value) setSelected(null);
            }}
            title={selected?.filename ?? corpus?.name ?? "Document corpus"}
            description={
                selected
                    ? "Full source text · Synthetic demo document"
                    : `${corpus?.documentCount ?? "—"} documents · ${corpus?.chunkCount.toLocaleString() ?? "—"} chunks · Local fixture index`
            }
        >
            {selected ? (
                <>
                    <Button
                        variant="ghost"
                        size="sm"
                        className="mt-4"
                        onClick={() => setSelected(null)}
                    >
                        <ArrowLeft size={14} />
                        Back to corpus
                    </Button>
                    <DocumentContent document={selected} />
                </>
            ) : (
                <div className="pt-5">
                    {error && <p role="alert" className="mb-3 text-destructive">{error}</p>}
                    <label className="relative block">
                        <Search
                            size={15}
                            className="absolute left-3 top-2.5 text-muted-foreground"
                        />
                        <input
                            aria-label="Search corpus files"
                            className="field !pl-9"
                            placeholder="Search documents…"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                    </label>
                    <div className="mt-4 space-y-2">
                        {filtered.map((doc) => (
                            <button
                                key={doc.id}
                                onClick={() => setSelected(doc)}
                                className="flex w-full items-start gap-3 rounded-lg border p-3 text-left hover:border-primary/40 hover:bg-accent/30"
                            >
                                <FileText
                                    size={17}
                                    className="mt-0.5 shrink-0 text-primary"
                                />
                                <div className="min-w-0 flex-1">
                                    <div className="truncate text-xs font-medium">
                                        {doc.filename}
                                    </div>
                                    <div className="mt-1 text-[11px] text-muted-foreground">
                                        {doc.title}
                                    </div>
                                </div>
                                <span className="mono shrink-0 text-[10px] text-muted-foreground">
                                    {doc.chunkCount} chunks
                                </span>
                            </button>
                        ))}
                        {filtered.length === 0 && (
                            <p className="py-8 text-center text-muted-foreground">
                                No documents match your search.
                            </p>
                        )}
                    </div>
                </div>
            )}
        </Drawer>
    );
}
