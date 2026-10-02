"use client";
import { useEffect, useRef, useState } from "react";
import { BookOpen, Check, Copy, Download, FileSearch, History, Loader2, Play, Square, Terminal } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Panel, PageHeading } from "@/components/shared/panel";
import { Drawer } from "@/components/shared/drawer";
import { Hint } from "@/components/shared/hint";
import { PipelineTrace } from "@/features/playground/pipeline-trace";
import type { TraceStage } from "@/lib/contracts";
import type { LiveQueryRequest, LiveQueryRun } from "@/lib/live/contracts";
import { liveGateway } from "@/lib/live/gateway";
import { downloadJson } from "@/lib/live/http";
import { useClientData } from "./use-client-data";
import { ClientError, ClientFooter, NoClientData, ProfileSelectors } from "./shared";
import { ClientDocuments } from "./client-documents";
import { initialQuerySettings, QueryControls } from "./query-controls";
import { LiveEvidence } from "./live-evidence";
import { LiveRetrievalChart } from "./retrieval-chart";

export function LiveQueryPage({ inspector = false }: { inspector?: boolean }) {
    const data = useClientData();
    const [question, setQuestion] = useState("");
    const [customSettings, setSettings] = useState<typeof initialQuerySettings | null>(null);
    const settings = customSettings ?? data.capabilities?.defaultQuery ?? initialQuerySettings;
    const [run, setRun] = useState<LiveQueryRun | null>(null);
    const [request, setRequest] = useState<LiveQueryRequest | null>(null);
    const [trace, setTrace] = useState<TraceStage[]>([]);
    const [running, setRunning] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [history, setHistory] = useState<{ run: LiveQueryRun; request: LiveQueryRequest }[]>([]);
    const [historyOpen, setHistoryOpen] = useState(false);
    const [expanded, setExpanded] = useState<string[]>([]);
    const [selected, setSelected] = useState<string | null>(null);
    const [document, setDocument] = useState<{ profileId: string; id?: string } | null>(null);
    const [copied, setCopied] = useState(false);
    const abort = useRef<AbortController | null>(null);
    useEffect(() => () => abort.current?.abort(), []);
    const profile = data.profiles.find(p => p.id === data.profileId);

    async function execute() {
        abort.current?.abort();
        const controller = new AbortController(); abort.current = controller;
        const snapshot = { ...settings, question: question.trim(), corpusId: data.corpusId, profileId: data.profileId, reranker: settings.reranker && !!data.capabilities?.reranker };
        setRunning(true); setError(""); setNotice(""); setRun(null); setTrace([]); setRequest(snapshot); setCopied(false);
        try {
            const result = await liveGateway.runQuery(snapshot, { signal: controller.signal, onStage(stage) {
                if (abort.current === controller) setTrace(current => current.some(s => s.id === stage.id) ? current.map(s => s.id === stage.id ? stage : s) : [...current, stage]);
            } });
            if (controller.signal.aborted || abort.current !== controller) return;
            setRun(result); setTrace(result.trace); setExpanded([]); setSelected(null);
            setHistory(current => [{ run: result, request: snapshot }, ...current].slice(0, 20));
        } catch (err) {
            if (abort.current !== controller) return;
            if (controller.signal.aborted) { setNotice("Query canceled. No result has been saved."); setTrace(current => current.map(s => s.status === "running" || s.status === "queued" ? { ...s, status: "canceled" } : s)); }
            else setError(err instanceof Error ? err.message : "Query failed.");
        } finally { if (abort.current === controller) setRunning(false); }
    }
    function reveal(id: string) {
        setSelected(id); setExpanded(current => [...new Set([...current, id])]);
        requestAnimationFrame(() => {
            const element = window.document.getElementById(`client-source-${id}`);
            element?.scrollIntoView({ block: "center", behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth" });
            element?.focus({ preventScroll: true });
        });
    }
    async function copy() {
        try { await navigator.clipboard.writeText(run?.answer ?? ""); setCopied(true); }
        catch { setNotice("Clipboard access is unavailable. Select the answer text to copy it."); }
    }
    const evidence = run ? [...run.candidates, ...run.context.filter(c => !run.candidates.some(candidate => candidate.id === c.id))] : [];
    return <>
        <PageHeading eyebrow="CLIENT ENVIRONMENT" title={inspector ? "Retrieval inspector" : "Playground"} description={inspector ? "Inspect actual candidate scores, context admission, and ranking changes." : "Query your documents through your configured models and retrieval services."} action={<Button variant="outline" size="sm" onClick={() => setHistoryOpen(true)}><History size={14} />Session history {history.length > 0 && `(${history.length})`}</Button>} />
        {data.error && <ClientError message={data.error} retry={data.refresh} />}
        {data.loading ? <Skeleton className="h-60" /> : !data.error && !data.corpora.length ? <NoClientData /> : !data.error && <div className="playground-grid">
            <div className="space-y-5"><Panel title="Query workspace" icon={<Terminal size={14} />} action={<Badge variant="outline" className="text-[9px]">Live API</Badge>}><form onSubmit={e => { e.preventDefault(); void execute(); }}><ProfileSelectors {...data} onCorpus={data.setCorpusId} onProfile={data.setProfileId} disabled={running} /><div className="mt-3 flex justify-end"><Button type="button" variant="ghost" size="sm" disabled={!profile} onClick={() => setDocument({ profileId: data.profileId })}><BookOpen size={12} />Browse corpus</Button></div><label className="mt-5 block"><span className="field-label">Ask a question</span><textarea required maxLength={8000} rows={5} className="field !text-xs !leading-6" disabled={running} value={question} onChange={e => setQuestion(e.target.value)} placeholder="Ask about the documents you indexed…" onKeyDown={e => { if ((e.metaKey || e.ctrlKey) && e.key === "Enter" && !running && question.trim() && profile?.chunkCount) { e.preventDefault(); void execute(); } }} /></label><div className="mt-4 flex gap-2">{running ? <Button type="button" className="flex-1" variant="outline" onClick={e => { e.preventDefault(); abort.current?.abort(); }}><Square size={12} />Cancel query</Button> : <Button type="submit" className="flex-1" disabled={!question.trim() || !profile?.chunkCount}><Play size={12} />Run Query</Button>}<Button type="button" variant="ghost" disabled={running} onClick={() => { setQuestion(""); setRun(null); setTrace([]); setError(""); setNotice(""); }}>Clear</Button></div>{profile && !profile.chunkCount && <p className="mt-3 text-[10px] text-muted-foreground">Ingest documents into this profile before querying.</p>}</form></Panel><Panel title="Retrieval configuration"><QueryControls settings={settings} onChange={setSettings} capabilities={data.capabilities} disabled={running} /></Panel></div>
            <div className="min-w-0 space-y-5">
                {error && <ClientError message={error} retry={() => void execute()} />}{notice && <p role="status" className="rounded border bg-card p-4 text-xs text-muted-foreground">{notice}</p>}
                <Panel title="Grounded Answer" icon={<FileSearch size={15} className="text-primary" />} action={run && <Button size="sm" variant="ghost" onClick={() => downloadJson(`query-${run.id}.json`, { request, run })}><Download size={12} />Export run</Button>}>
                    {running ? <div role="status" className="space-y-4 py-8"><p className="flex items-center gap-2 text-xs text-primary"><Loader2 size={15} className="animate-spin" />Executing against your environment…</p><Skeleton className="h-3" /><Skeleton className="h-3 w-5/6" /><Skeleton className="h-3 w-2/3" /></div> : run ? <><div className="mb-4 flex items-center gap-2 text-[10px] text-muted-foreground"><Badge variant="outline">{run.abstained ? "Insufficient evidence" : "Generated answer"}</Badge><span className="mono ml-auto">{run.totalLatencyMs.toFixed(1)} ms total</span></div><div className="whitespace-pre-wrap text-[13px] leading-7">{run.answer.split(/(\[[^\]\n]+\])/g).map((part, index) => { const identifier = part.startsWith("[") ? part.slice(1, -1) : null; const citation = identifier ? run.citations.find(c => c.valid && (c.chunkId === identifier || String(c.number) === identifier)) : null; return citation ? <button key={index} aria-label={`Citation ${citation.number}: show source`} className="mx-1 rounded bg-accent px-1.5 font-mono text-[10px] text-primary" onClick={() => reveal(citation.chunkId)}>[{citation.number}]</button> : <span key={index}>{part}</span>; })}</div>{run.invalidCitations.length > 0 && <p role="alert" className="mt-4 rounded border border-destructive/30 p-3 text-xs text-destructive">{run.invalidCitations.length} citation reference(s) did not resolve to admitted context. Reference validation does not establish factual support.</p>}<div className="mt-5 flex flex-wrap items-center gap-4 border-t pt-4 text-[10px] text-muted-foreground"><span>{run.citations.filter(c => c.valid).length} valid citations<Hint text="A valid reference resolves to an admitted chunk. This check does not prove that the cited text supports every claim." /></span><span>{run.context.length} context chunks</span><span>{run.contextTokens} context tokens (est.) · {run.outputTokens} output tokens {run.tokenUsageKind.startsWith("provider") ? "(provider)" : "(est.)"}{run.totalTokens != null && <Hint text={`Provider total: ${run.totalTokens}; prompt: ${run.promptTokens ?? "unavailable"}. Context budgeting uses a separate characters ÷ 4 estimate.`} />}</span><Button className="ml-auto" size="sm" variant="ghost" onClick={() => void copy()}>{copied ? <Check size={12} /> : <Copy size={12} />}{copied ? "Copied" : "Copy answer"}</Button></div></> : <div className="py-14 text-center"><FileSearch className="mx-auto mb-4 text-primary" size={28} /><h2 className="text-sm font-semibold">Evidence from your environment</h2><p className="mx-auto mt-2 max-w-sm text-xs leading-6 text-muted-foreground">Select an indexed profile and run a question. Actual source passages, scores, citations, and stage timings will appear here.</p></div>}
                </Panel>
                {run && <><Panel><LiveRetrievalChart chunks={run.candidates} threshold={request?.minRelevance ?? 0} selected={selected} onSelect={reveal} /></Panel>{inspector && selected && <Panel title="Query / chunk comparison"><div className="grid gap-4 sm:grid-cols-2"><div><h3 className="eyebrow mb-2">Query</h3><p className="text-xs leading-6">{run.question}</p></div><div><h3 className="eyebrow mb-2">Selected evidence</h3><HighlightedText query={run.question} text={evidence.find(c => c.id === selected)?.content ?? ""} /><p className="mt-3 text-[10px] text-muted-foreground">Highlights show lexical overlap, not a semantic attribution score.</p></div></div></Panel>}<section><h2 className="panel-title mb-3"><BookOpen size={15} />Retrieved Context<span className="mono ml-auto text-[10px] text-muted-foreground">{run.context.length} admitted / {run.candidates.length} retrieved</span></h2><LiveEvidence chunks={evidence} expanded={expanded} selected={selected} onToggle={id => { setSelected(id); setExpanded(current => current.includes(id) ? current.filter(item => item !== id) : [...current, id]); }} onDocument={id => setDocument({ profileId: run.profileId, id })} /></section></>}
                <PipelineTrace stages={trace} running={running} simulated={false} totalLatencyMs={run?.totalLatencyMs} />
            </div>
        </div>}
        {document && <ClientDocuments key={`${document.profileId}-${document.id}`} profileId={document.profileId} documentId={document.id} onClose={() => setDocument(null)} />}
        <Drawer open={historyOpen} onOpenChange={setHistoryOpen} title="Client query history" description="Up to 20 runs held in memory on this page. Export any result to save it explicitly."><div className="space-y-3 py-5">{!history.length && <p className="text-xs text-muted-foreground">No client queries in this session.</p>}{history.map(item => <button className="block w-full rounded-lg border p-4 text-left hover:bg-accent/30" key={item.run.id} onClick={() => { abort.current?.abort(); abort.current = null; setRunning(false); setRun(item.run); setRequest(item.request); setTrace(item.run.trace); setQuestion(item.request.question); setSettings(item.request); data.setCorpusId(item.request.corpusId); data.setProfileId(item.request.profileId); setHistoryOpen(false); setError(""); }}><p className="text-xs leading-6">{item.run.question}</p><p className="mono mt-2 text-[10px] text-muted-foreground">{item.request.mode} · Top {item.request.topK} · {item.run.totalLatencyMs.toFixed(1)} ms</p></button>)}</div></Drawer><ClientFooter />
    </>;
}
function HighlightedText({ text, query }: { text: string; query: string }) {
    const words = new Set(query.toLowerCase().match(/[\p{L}\p{N}]{3,}/gu) ?? []);
    return <p className="text-xs leading-6">{text.split(/([\p{L}\p{N}]+)/gu).map((part, index) => words.has(part.toLowerCase()) ? <mark key={index} className="rounded bg-accent px-0.5 text-primary">{part}</mark> : part)}</p>;
}
